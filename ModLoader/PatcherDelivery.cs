#nullable enable
using ModLoader;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

// NuGet: BouncyCastle.Cryptography  (Ed25519 + BLAKE2b for minisign; .NET has no Ed25519)

namespace ModManager
{
    // ---------------------------------------------------------------- config
    internal static class PatcherConfig
    {
        // Inside PatcherConfig:
        public static string AppDataRoot => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cslol-go");

        public static string Root => Path.Combine(AppDataRoot, "patcher");
        public static string ProfilesRoot => Path.Combine(AppDataRoot, "profiles");
        public static string LogFilePath => Path.Combine(AppDataRoot, "patcher_log.txt");
        public const string Client = "cslol-go";
        public const int HostProtocol = 3;

#if PATCHER_STAGING
        public const string BaseUrl = "https://patcher-staging.leaguetoolkit.dev";
        public const string ManifestKey = "RWRHh5bl7JOIscd3lf1Sw2xqd5qcoSeau1GmXjBm7C4KA4YSB0DOf+Kx";
#else
        public const string BaseUrl = "https://patcher.leaguetoolkit.dev";
        public const string ManifestKey = "RWSk74swcHfkZfLblm3iSk9RLVHJ1xjtqn/zySIPaCBM1hecnlFy1Bxi";
#endif

        // Channels LTK granted you, in preference order. Falls back left to right.
        // If you were only granted one, list only that one.
        public static readonly string[] Channels = { "beta", "stable" };

        public const string DllName = "ltk_patcher_dll.dll";
        public const string HostName = "ltk_patcher_host.exe";

        // Single source of truth: <Version>x.y.z</Version> in the csproj.
        public static string Version
        {
            get
            {
                var v = typeof(PatcherConfig).Assembly.GetName().Version!;
                return $"{v.Major}.{v.Minor}.{Math.Max(v.Build, 0)}";
            }
        }
        public static string BundledHost => Path.Combine(AppContext.BaseDirectory, "cslol-tools", HostName);
    }

    public sealed class PatcherException : Exception
    {
        public bool Fatal { get; }          // stop for this session, do not retry
        public string? Reason { get; }      // "reason" field of the service error body
        public PatcherException(string message, bool fatal = false, string? reason = null) : base(message)
        { Fatal = fatal; Reason = reason; }
    }

    public sealed record PreparedPatcher(bool Ok, string? Error, string? HostPath, string? DllPath, string? ReleaseVersion);

    // ---------------------------------------------------------------- models
    internal sealed class Envelope
    {
        [JsonPropertyName("manifest")] public string Manifest { get; set; } = "";
        [JsonPropertyName("signature")] public string Signature { get; set; } = "";
    }

    internal sealed class ReleaseInfo
    {
        [JsonPropertyName("version")] public string Version { get; set; } = "";
        [JsonPropertyName("digest")] public string Digest { get; set; } = "";
        [JsonPropertyName("size")] public long Size { get; set; }
        [JsonPropertyName("protocol")] public int Protocol { get; set; }
        [JsonPropertyName("eol")] public long Eol { get; set; }
    }

    internal sealed class ManifestBody
    {
        [JsonPropertyName("schema")] public int Schema { get; set; }
        [JsonPropertyName("channel")] public string Channel { get; set; } = "";
        [JsonPropertyName("sequence")] public long Sequence { get; set; }
        [JsonPropertyName("release")] public ReleaseInfo Release { get; set; } = new();
        [JsonPropertyName("revoked")] public List<string> Revoked { get; set; } = new();
    }

    internal sealed class ChannelState
    {
        [JsonPropertyName("envelope")] public Envelope? Envelope { get; set; }
        [JsonPropertyName("etag")] public string? ETag { get; set; }
    }

    internal sealed class StoreState
    {
        [JsonPropertyName("install")] public string InstallId { get; set; } = "";
        [JsonPropertyName("token")] public string? Token { get; set; }
        [JsonPropertyName("tokenExpires")] public DateTimeOffset? TokenExpires { get; set; }
        [JsonPropertyName("channels")] public Dictionary<string, ChannelState> Channels { get; set; } = new();
        [JsonPropertyName("revoked")] public HashSet<string> Revoked { get; set; } = new();
    }

    // ---------------------------------------------------------------- minisign
    internal static class Minisign
    {
        public static bool Verify(byte[] data, string sigText, string pubKeyB64, out string trustedComment)
        {
            trustedComment = "";
            try
            {
                byte[] pk = Convert.FromBase64String(pubKeyB64);                    // "Ed" + keyid(8) + key(32)
                if (pk.Length != 42 || pk[0] != (byte)'E' || pk[1] != (byte)'d') return false;

                string[] lines = sigText.Replace("\r", "").Split('\n');
                if (lines.Length < 4 || !lines[2].StartsWith("trusted comment: ", StringComparison.Ordinal)) return false;

                byte[] sig = Convert.FromBase64String(lines[1].Trim());             // algo(2) + keyid(8) + sig(64)
                byte[] gsig = Convert.FromBase64String(lines[3].Trim());            // 64
                if (sig.Length != 74 || gsig.Length != 64) return false;

                for (int i = 0; i < 8; i++) if (pk[2 + i] != sig[2 + i]) return false; // key id

                bool prehashed;
                if (sig[0] == (byte)'E' && sig[1] == (byte)'d') prehashed = false;
                else if (sig[0] == (byte)'E' && sig[1] == (byte)'D') prehashed = true;
                else return false;

                byte[] pub = pk[10..42];
                byte[] sigBytes = sig[10..74];

                byte[] message = data;
                if (prehashed)
                {
                    var d = new Blake2bDigest(512);
                    d.BlockUpdate(data, 0, data.Length);
                    message = new byte[64];
                    d.DoFinal(message, 0);
                }
                if (!Ed25519Verify(pub, message, sigBytes)) return false;

                string comment = lines[2].Substring("trusted comment: ".Length);
                byte[] global = sigBytes.Concat(Encoding.UTF8.GetBytes(comment)).ToArray();
                if (!Ed25519Verify(pub, global, gsig)) return false;

                trustedComment = comment;
                return true;
            }
            catch { return false; }
        }

        private static bool Ed25519Verify(byte[] pub, byte[] msg, byte[] sig)
        {
            var v = new Ed25519Signer();
            v.Init(false, new Ed25519PublicKeyParameters(pub, 0));
            v.BlockUpdate(msg, 0, msg.Length);
            return v.VerifySignature(sig);
        }
    }

    // ---------------------------------------------------------------- delivery
    public static class PatcherDelivery
    {
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private static readonly Regex HexDigest = new("^[0-9a-f]{64}$", RegexOptions.Compiled);
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
        private static string? _fatal;                    // set on a fatal service error, lasts for this run
        private static DateTime _retryAfterUtc = DateTime.MinValue;

        private static readonly HttpClient Http = CreateHttp();

        private static HttpClient CreateHttp()
        {
            var handler = new SocketsHttpHandler { ConnectTimeout = TimeSpan.FromSeconds(15) };
            var c = new HttpClient(handler)
            {
                BaseAddress = new Uri(PatcherConfig.BaseUrl),
                Timeout = TimeSpan.FromSeconds(120)
            };
            // Exactly this and nothing else, or every route answers an empty 404.
            c.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", $"{PatcherConfig.Client}/{PatcherConfig.Version}");
            return c;
        }

        // ------------------------------------------------------------ public API

        /// <summary>Call once at app start: cleans the store, then syncs now and hourly in the background.</summary>
        public static void Initialize(CancellationToken ct = default)
        {
            try { Directory.CreateDirectory(PatcherConfig.Root); CleanupStore(LoadState()); } catch { }

            _ = Task.Run(async () =>
            {
                try
                {
                    await SyncAsync(ct);
                    using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
                    while (await timer.WaitForNextTickAsync(ct)) await SyncAsync(ct);
                }
                catch (OperationCanceledException) { }
            }, ct);
        }

        /// <summary>One sync pass. Returns null on success, otherwise a user-presentable error.</summary>
        public static async Task<string?> SyncAsync(CancellationToken ct = default)
        {
            if (_fatal != null) return _fatal;
            if (DateTime.UtcNow < _retryAfterUtc) return "The patcher service asked us to wait. Try again shortly.";

            await Gate.WaitAsync(ct);
            StoreState? state = null;
            try
            {
                state = LoadState();
                bool any = false;
                foreach (string channel in PatcherConfig.Channels)
                {
                    if (await SyncChannelAsync(state, channel, ct)) { any = true; break; }
                }
                return any ? null : "No compatible patcher build is available right now.";
            }
            catch (PatcherException ex)
            {
                if (ex.Fatal) _fatal = ex.Message;
                return ex.Message;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is IOException)
            {
                return $"Could not reach the patcher service: {ex.Message}";
            }
            finally
            {
                if (state != null) { try { SaveState(state); } catch { } }
                Gate.Release();
            }
        }

        /// <summary>
        /// Run before every session. Does the preflight, makes sure a verified, non-expired DLL is on disk
        /// (syncing and waiting if not), and puts the bundled host beside it.
        /// </summary>
        public static async Task<PreparedPatcher> PrepareSessionAsync(CancellationToken ct = default)
        {
            string? blocked = PatcherPreflight.Check();
            if (blocked != null) return new(false, blocked, null, null, null);

            if (!File.Exists(PatcherConfig.BundledHost))
                return new(false, $"Bundled {PatcherConfig.HostName} is missing. Reinstall the app.", null, null, null);

            var usable = SelectUsable(LoadState(), allowExpired: false);
            string? syncError = null;
            if (usable == null)
            {
                syncError = await SyncAsync(ct);                       // wait for it
                usable = SelectUsable(LoadState(), allowExpired: false);
            }
            else
            {
                _ = SyncAsync(ct);                                     // applies to the next session
            }

            if (usable == null)
                return new(false, syncError ?? "No valid patcher DLL is available.", null, null, null);

            try
            {
                string dir = Path.Combine(PatcherConfig.Root, usable.Release.Digest);
                string host = EnsureHost(dir);
                return new(true, null, host, Path.Combine(dir, PatcherConfig.DllName), usable.Release.Version);
            }
            catch (Exception ex)
            {
                return new(false, $"Could not prepare the patcher host: {ex.Message}", null, null, null);
            }
        }

        // ------------------------------------------------------------ sync internals

        // true = this channel has a verified release whose DLL is on disk
        private static async Task<bool> SyncChannelAsync(StoreState s, string channel, CancellationToken ct)
        {
            if (!s.Channels.TryGetValue(channel, out var cs)) s.Channels[channel] = cs = new ChannelState();

            ManifestBody? stored = cs.Envelope == null ? null : SafeVerify(cs.Envelope, channel);
            if (stored == null) { cs.Envelope = null; cs.ETag = null; }

            string path = $"/v1/offer?channel={Uri.EscapeDataString(channel)}&version={Uri.EscapeDataString(PatcherConfig.Version)}";
            using var resp = await SendAuthedAsync(s, () =>
            {
                var r = new HttpRequestMessage(HttpMethod.Get, path);
                if (stored != null && cs.ETag != null) r.Headers.TryAddWithoutValidation("If-None-Match", cs.ETag);
                return r;
            }, ct);

            ManifestBody body;
            switch (resp.StatusCode)
            {
                case HttpStatusCode.NotModified:
                    if (stored == null) throw new PatcherException("Service answered 304 but we hold no manifest.");
                    body = stored;
                    break;

                case HttpStatusCode.OK:
                    {
                        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                        var env = doc.RootElement.GetProperty("manifest").Deserialize<Envelope>()
                                  ?? throw new PatcherException("Malformed offer.");
                        var fresh = VerifyEnvelope(env, channel);           // throws on any failure
                        if (stored != null && fresh.Sequence < stored.Sequence)
                        {
                            body = stored;                                  // refuse the rollback, keep the old one
                        }
                        else
                        {
                            body = fresh;
                            cs.Envelope = env;
                            cs.ETag = resp.Headers.ETag?.ToString();
                        }
                        break;
                    }

                default:
                    {
                        var ex = await FailAsync(resp, ct);
                        if (ex.Reason == "incompatible" || ex.Reason == "channel") return false; // try the next channel
                        throw ex;
                    }
            }

            foreach (string d in body.Revoked) s.Revoked.Add(d.ToLowerInvariant());
            SaveState(s);
            if (s.Revoked.Contains(body.Release.Digest)) return false;

            await EnsureDllAsync(s, body.Release, ct);
            return true;
        }

        private static ManifestBody VerifyEnvelope(Envelope env, string channel)
        {
            byte[] bytes = Convert.FromBase64String(env.Manifest);

            // Verify before parse.
            if (!Minisign.Verify(bytes, env.Signature, PatcherConfig.ManifestKey, out string comment))
                throw new PatcherException("Patcher manifest signature is invalid.", fatal: true);

            var body = JsonSerializer.Deserialize<ManifestBody>(bytes) ?? throw new PatcherException("Malformed manifest.", true);

            if (comment != $"{body.Channel} {body.Sequence} {body.Release.Version}")
                throw new PatcherException("Patcher manifest trusted comment does not match its body.", true);
            if (body.Schema != 1) throw new PatcherException("Unsupported manifest schema.", true);
            if (body.Channel != channel) throw new PatcherException("Manifest is for a different channel.", true);
            if (body.Release.Protocol != PatcherConfig.HostProtocol) throw new PatcherException("Manifest protocol mismatch.", true);

            body.Release.Digest = body.Release.Digest.ToLowerInvariant();
            if (!HexDigest.IsMatch(body.Release.Digest)) throw new PatcherException("Manifest digest is malformed.", true);
            return body;
        }

        private static ManifestBody? SafeVerify(Envelope env, string channel)
        {
            try { return VerifyEnvelope(env, channel); } catch { return null; }
        }

        private static async Task EnsureDllAsync(StoreState s, ReleaseInfo rel, CancellationToken ct)
        {
            string dir = Path.Combine(PatcherConfig.Root, rel.Digest);       // digest is validated hex
            string final = Path.Combine(dir, PatcherConfig.DllName);
            Directory.CreateDirectory(dir);

            if (File.Exists(final) && HashFile(final) == rel.Digest) return;

            string tmp = Path.Combine(dir, PatcherConfig.DllName + "." + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using var resp = await SendAuthedAsync(s,
                    () => new HttpRequestMessage(HttpMethod.Get, $"/v1/dll/{rel.Digest}"), ct, HttpCompletionOption.ResponseHeadersRead);

                if (resp.StatusCode == HttpStatusCode.NotFound)
                    throw new PatcherException("The patcher build is no longer live (404).");
                if (!resp.IsSuccessStatusCode) throw await FailAsync(resp, ct);
                if (resp.Content.Headers.ContentLength is long cl && cl > rel.Size)
                    throw new PatcherException("Patcher download is larger than the manifest says.");

                using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long total = 0;
                await using (var src = await resp.Content.ReadAsStreamAsync(ct))
                await using (var fs = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.None))
                {
                    var buf = new byte[81920];
                    int n;
                    while ((n = await src.ReadAsync(buf, ct)) > 0)
                    {
                        total += n;
                        if (total > rel.Size) throw new PatcherException("Patcher download exceeded the expected size.");
                        sha.AppendData(buf, 0, n);
                        await fs.WriteAsync(buf.AsMemory(0, n), ct);
                    }
                    fs.Flush(flushToDisk: true);                                  // fsync
                }

                string got = Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant();
                if (total != rel.Size || got != rel.Digest)
                    throw new PatcherException("Patcher download failed verification.");

                File.Move(tmp, final, overwrite: true);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        // ------------------------------------------------------------ session helpers

        private static ManifestBody? SelectUsable(StoreState s, bool allowExpired)
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (string ch in PatcherConfig.Channels)
            {
                if (!s.Channels.TryGetValue(ch, out var cs) || cs.Envelope == null) continue;
                var body = SafeVerify(cs.Envelope, ch);                            // re-verify every time
                if (body == null) continue;
                var rel = body.Release;
                if (s.Revoked.Contains(rel.Digest) || rel.Protocol != PatcherConfig.HostProtocol) continue;
                if (!allowExpired && rel.Eol > 0 && rel.Eol <= now) continue;
                string dll = Path.Combine(PatcherConfig.Root, rel.Digest, PatcherConfig.DllName);
                if (!File.Exists(dll) || HashFile(dll) != rel.Digest) continue;    // never run an unverified file
                return body;
            }
            return null;
        }

        private static string EnsureHost(string dir)
        {
            string target = Path.Combine(dir, PatcherConfig.HostName);
            if (!File.Exists(target) || HashFile(target) != HashFile(PatcherConfig.BundledHost))
                File.Copy(PatcherConfig.BundledHost, target, overwrite: true);
            return target;
        }

        private static void CleanupStore(StoreState s)
        {
            var keep = new HashSet<string>();
            foreach (var (ch, cs) in s.Channels)
            {
                var b = cs.Envelope == null ? null : SafeVerify(cs.Envelope, ch);
                if (b != null) keep.Add(b.Release.Digest);
            }
            foreach (string d in Directory.EnumerateDirectories(PatcherConfig.Root))
            {
                string name = Path.GetFileName(d);
                if (!HexDigest.IsMatch(name) || keep.Contains(name)) continue;
                try { Directory.Delete(d, recursive: true); } catch { /* in use by a running host: skip */ }
            }
        }

        // ------------------------------------------------------------ registration + http

        private static bool NeedsToken(StoreState s) =>
            string.IsNullOrEmpty(s.Token) || s.TokenExpires == null || s.TokenExpires < DateTimeOffset.UtcNow.AddDays(1);

        private static async Task<HttpResponseMessage> SendAuthedAsync(
            StoreState s, Func<HttpRequestMessage> make, CancellationToken ct,
            HttpCompletionOption option = HttpCompletionOption.ResponseContentRead)
        {
            for (int attempt = 0; ; attempt++)
            {
                if (NeedsToken(s)) await RegisterAsync(s, ct);
                var req = make();
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s.Token);
                var resp = await Http.SendAsync(req, option, ct);
                if (resp.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    resp.Dispose();
                    s.Token = null;                                                // register again, retry once
                    continue;
                }
                return resp;
            }
        }

        private static async Task RegisterAsync(StoreState s, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(BuildAttestation.Value))
                throw new PatcherException("This build carries no attestation, so it cannot register with the patcher service.", true);

            for (int attempt = 0; ; attempt++)
            {
                using var cr = await Http.GetAsync($"/v1/challenge?install={s.InstallId}", ct);
                if (!cr.IsSuccessStatusCode) throw await FailAsync(cr, ct);

                using var cdoc = JsonDocument.Parse(await cr.Content.ReadAsStringAsync(ct));
                string challenge = cdoc.RootElement.GetProperty("challenge").GetString()!;
                int difficulty = cdoc.RootElement.GetProperty("difficulty").GetInt32();
                if (difficulty > 24)
                    throw new PatcherException("The patcher service is asking for too much work from this network. Try again later.");

                string nonce = await Task.Run(() => SolvePow(challenge, difficulty, ct), ct);

                string payload = JsonSerializer.Serialize(new
                {
                    install = s.InstallId,
                    client = PatcherConfig.Client,
                    version = PatcherConfig.Version,
                    attestation = BuildAttestation.Value,
                    challenge,
                    nonce
                });
                using var rr = await Http.PostAsync("/v1/installs", new StringContent(payload, Encoding.UTF8, "application/json"), ct);

                if (rr.IsSuccessStatusCode)
                {
                    using var rdoc = JsonDocument.Parse(await rr.Content.ReadAsStringAsync(ct));
                    s.Token = rdoc.RootElement.GetProperty("token").GetString();
                    s.TokenExpires = DateTimeOffset.Parse(rdoc.RootElement.GetProperty("expires").GetString()!,
                        CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
                    SaveState(s);
                    return;
                }

                var ex = await FailAsync(rr, ct);
                if (ex.Reason == "challenge" && attempt < 1) continue;            // fetch a new challenge
                throw ex;
            }
        }

        private static async Task<PatcherException> FailAsync(HttpResponseMessage r, CancellationToken ct)
        {
            string body = "";
            try { body = await r.Content.ReadAsStringAsync(ct); } catch { }

            string? reason = null;
            try
            {
                using var d = JsonDocument.Parse(body);
                if (d.RootElement.TryGetProperty("reason", out var p)) reason = p.GetString();
            }
            catch { }

            switch ((int)r.StatusCode)
            {
                case 403:
                    return reason switch
                    {
                        "attestation" => new("Patcher service rejected this build's attestation. This build is broken; please update.", true, reason),
                        "client" => new("This version of the app is no longer allowed to fetch the patcher. Please update.", true, reason),
                        "install" => new("This installation was denied by the patcher service.", true, reason),
                        _ => new($"Patcher service refused the request ({reason ?? "403"}).", false, reason)
                    };
                case 409:
                    return new("No live patcher build speaks this app's host protocol.", false, reason ?? "incompatible");
                case 429:
                    {
                        int secs = r.Headers.RetryAfter?.Delta is TimeSpan ts ? (int)ts.TotalSeconds : 300;
                        _retryAfterUtc = DateTime.UtcNow.AddSeconds(Math.Max(secs, 1));
                        return new($"Patcher service is rate limiting us; wait {secs}s.", false, "rate_limited");
                    }
                case 404:
                    return new("Patcher service answered 404 (wrong User-Agent, unknown path, or build not live).", false, "404");
                case 503:
                    return new("Patcher service is unavailable. Try again later.", false, "unavailable");
                default:
                    return new($"Patcher service error {(int)r.StatusCode}.", false, reason);
            }
        }

        // ------------------------------------------------------------ proof of work

        private static string SolvePow(string challenge, int difficulty, CancellationToken ct)
        {
            byte[] prefix = Encoding.ASCII.GetBytes(challenge);
            byte[] buf = new byte[prefix.Length + 16];
            Buffer.BlockCopy(prefix, 0, buf, 0, prefix.Length);
            Span<byte> hash = stackalloc byte[32];

            for (ulong n = 0; ; n++)
            {
                if ((n & 0xFFFF) == 0) ct.ThrowIfCancellationRequested();
                int len = WriteHex(n, buf, prefix.Length);
                SHA256.HashData(buf.AsSpan(0, prefix.Length + len), hash);
                if (LeadingZeroBits(hash) >= difficulty)
                    return Encoding.ASCII.GetString(buf, prefix.Length, len);
            }
        }

        private static int WriteHex(ulong n, byte[] b, int off)
        {
            int digits = 1;
            for (ulong t = n >> 4; t != 0; t >>= 4) digits++;
            for (int i = digits - 1; i >= 0; i--, n >>= 4) b[off + i] = (byte)"0123456789abcdef"[(int)(n & 0xF)];
            return digits;
        }

        private static int LeadingZeroBits(ReadOnlySpan<byte> h)
        {
            int bits = 0;
            foreach (byte x in h)
            {
                if (x == 0) { bits += 8; continue; }
                bits += System.Numerics.BitOperations.LeadingZeroCount((uint)x) - 24;
                break;
            }
            return bits;
        }

        // ------------------------------------------------------------ state file

        private static string StatePath => Path.Combine(PatcherConfig.Root, "delivery.json");

        private static StoreState LoadState()
        {
            Directory.CreateDirectory(PatcherConfig.Root);
            StoreState? s = null;
            try { if (File.Exists(StatePath)) s = JsonSerializer.Deserialize<StoreState>(File.ReadAllText(StatePath)); }
            catch { /* unparseable: discard and start over */ }

            s ??= new StoreState();
            if (string.IsNullOrEmpty(s.InstallId)) s.InstallId = Guid.NewGuid().ToString("D").ToLowerInvariant();
            return s;
        }

        private static void SaveState(StoreState s)
        {
            Directory.CreateDirectory(PatcherConfig.Root);
            string tmp = StatePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, JsonOpts), new UTF8Encoding(false));
            File.Move(tmp, StatePath, overwrite: true);                           // atomic replace
        }

        private static string HashFile(string path)
        {
            using var f = File.OpenRead(path);
            return Convert.ToHexString(SHA256.HashData(f)).ToLowerInvariant();
        }
    }
}