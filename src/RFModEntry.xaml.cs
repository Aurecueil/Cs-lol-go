// using HtmlAgilityPack;
// using ModManager;
// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Linq;
// using System.Net.Http;
// using System.Text;
// using System.Text.Json;
// using System.Threading.Tasks;
// using System.Windows;
// using System.Windows.Controls;
// using System.Windows.Data;
// using System.Windows.Documents;
// using System.Windows.Input;
// using System.Windows.Media;
// using System.Windows.Media.Imaging;
// using System.Windows.Navigation;
// using System.Windows.Shapes;
// using Path = System.IO.Path;

// namespace ModManager
// {
//     public partial class RFModEntry : UserControl
//     {
//         private MainWindow.RFMod _mod;
//         private string modId;
//         private string new_name;
//         public RFModEntry(MainWindow.RFMod mod, string ID)
//         {
//             InitializeComponent();
//             _mod = mod;
//             modId = ID;
//             new_name = $".rf---{ID}";
//             EntryName.Text = mod.Name;
//             DetailsText.Text = mod.Author;
//             if (Directory.Exists(Path.Combine("installed", new_name)))
//             {
//                 dl_button.Text = "O";
//                 DeleteIcon.IsEnabled = false;
//             }
//             set_thum(mod.Image);
//         }
// 
//         private BitmapImage bitmapImage;
//         private async void set_thum(string link)
//         {
//             bitmapImage = new BitmapImage(new Uri(link));
//             BackgroundBorder.Background = new ImageBrush(bitmapImage) { Stretch = Stretch.UniformToFill, Opacity = 0.6 };
//         }
//         private async void Delete_click_clicked(object sender, RoutedEventArgs e) {
//             DeleteIcon.IsEnabled = false;
//             dl_button.Text = "-";
//             HttpClient client = new HttpClient();
// 
//             try
//             {
//                 string release = await GetLatestRelease(client);
//                 string DL_LINK = $"https://runeforge.dev/mods/{modId}/releases/{release}/download";
// 
// 
//                 string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
//                 string uniqueFolder = Path.Combine(appDataPath, Guid.NewGuid().ToString());
//                 Directory.CreateDirectory(uniqueFolder);
// 
//                 string targetFile = Path.Combine(uniqueFolder, $"{new_name}.fantome");
//                 byte[] fileBytes = await client.GetByteArrayAsync(DL_LINK);
//                 await File.WriteAllBytesAsync(targetFile, fileBytes);
// 
//                 await Task.Run(() =>
//                 {
//                     Application.Current.Dispatcher.Invoke(() =>
//                     {
//                         if (Application.Current.MainWindow is MainWindow mainWindow)
//                         {
//                             mainWindow.HandleDroppedItem(targetFile);
//                         }
//                     });
//                 });
//                 dl_button.Text = "O";
//                 Directory.Delete(uniqueFolder, true);
//                 string installedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installed", new_name, "META");
//                 string releaseTxtPath = Path.Combine(installedPath, "release.txt");
//                 await File.WriteAllTextAsync(releaseTxtPath, release);
//             }
//             catch (Exception ex) { }
//             
//         }
// 
//         async Task<string> GetLatestRelease(HttpClient client)
//         {
//             string url = $"https://runeforge.dev/mods/{modId}";
//             Console.WriteLine($"Fetching details: {url}");
//             string html = await client.GetStringAsync(url);
// 
//             HtmlDocument doc = new HtmlDocument();
//             doc.LoadHtml(html);
// 
//             var releaseNode = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '/releases/') and contains(@href, '/download')]");
//             if (releaseNode != null)
//             {
//                 string href = releaseNode.GetAttributeValue("href", "");
//                 return href.Split("/releases/")[1].Split('/')[0];
//             }
//             else
//             {
//                 return null; // or return string.Empty;
//             }
//         }
// 
//     }
// }

//
// public class RFMod
// {
//     public string Name { get; set; }
//     public string Author { get; set; }
//     public string Image { get; set; }
//     public string LatestRelease { get; set; }
// }
// async Task<Dictionary<string, RFMod>> FetchModsList(HttpClient client, Dictionary<string, RFMod> mods)
// {
//     Dictionary<string, RFMod> Updated_mods = new();
//     int page = 0;
//     bool stop = false;
// 
//     while (!stop)
//     {
//         string url = $"{baseUrl}/mods?page={page}&pageSize=72";
//         Console.WriteLine($"Fetching: {url}");
//         string html = await client.GetStringAsync(url);
// 
//         HtmlDocument doc = new HtmlDocument();
//         doc.LoadHtml(html);
// 
//         var modCards = doc.DocumentNode.SelectNodes("//a[contains(@href, '/mods/') and img]");
//         if (modCards == null || modCards.Count == 0)
//         {
//             Console.WriteLine("No more mods found.");
//             break;
//         }
// 
//         foreach (var link in modCards)
//         {
//             string href = link.GetAttributeValue("href", "");
//             if (string.IsNullOrEmpty(href) || !href.Contains("/mods/")) continue;
// 
//             string id = href.Split("/mods/")[1].Split('/')[0];
//             if (mods.ContainsKey(id))
//             {
//                 Console.WriteLine($"Already in list, stopping at ID: {id}");
//                 stop = true;
//                 break;
//             }
// 
//             // Instead of link.ParentNode, find the closest parent <div class="group ...">
//             var cardContainer = link.Ancestors("div")
//                 .FirstOrDefault(div => div.GetClasses().Contains("group"));
//             if (cardContainer == null)
//             {
//                 Console.WriteLine($"No card container found for mod {id}");
//                 continue;
//             }
// 
//             var imageLink = cardContainer.SelectSingleNode(".//a[contains(@href, '/mods/')]");
//             var imgNode = imageLink?.SelectSingleNode(".//img");
// 
//             // Try to get 'src' attribute or 'data-cfsrc' fallback (Cloudflare often uses this attribute)
//             string image = imgNode?.GetAttributeValue("src", "") ?? "";
//             if (string.IsNullOrEmpty(image))
//             {
//                 image = imgNode?.GetAttributeValue("data-cfsrc", "") ?? "";
//             }
// 
//             if (!string.IsNullOrEmpty(image))
//             {
//                 Console.WriteLine($"Original IMAGE SRC: {image}");
// 
//                 // Extract real image URL from Cloudflare proxy if present
//                 int httpsIndex = image.IndexOf("https://");
//                 if (httpsIndex >= 0)
//                 {
//                     image = image.Substring(httpsIndex);
//                 }
//                 else if (image.StartsWith("/"))
//                 {
//                     // Optional: prepend base URL if the image is relative
//                     // string baseUrl = "https://rune-forge.com"; // example base URL
//                     // image = baseUrl + image;
//                 }
// 
//                 Console.WriteLine($"Extracted Real Image URL: {image}");
//             }
//             else
//             {
//                 Console.WriteLine("No image found.");
//             }
// 
// 
//             // Extract Name - look for <a> with class containing 'font-bold' or 'text-lg' inside card container
//             var nameNode = cardContainer.SelectSingleNode(".//a[contains(@class,'font-bold') or contains(@class,'text-lg')]");
//             string name = nameNode?.InnerText.Trim() ?? "Unknown";
// 
//             // Extract Author - find <a href="/users/..."> inside card container
//             var authorNode = cardContainer.SelectSingleNode(".//a[contains(@href, '/users/')]");
//             string author = authorNode?.InnerText.Trim() ?? "Unknown";
// 
//             Updated_mods[id] = new RFMod
//             {
//                 Name = name,
//                 Author = author,
//                 Image = image,
//                 LatestRelease = ""
//             };
// 
//             Console.WriteLine($"Added: {name} by {author} [{id}]");
//         }
// 
//         page++;
//     }
//     foreach (var kvp in mods)
//     {
//         Updated_mods[kvp.Key] = kvp.Value;
//     }
//     return Updated_mods;
// }
// private string baseUrl = "https://runeforge.dev";
// private string jsonPath = "mods.json";
// 
// HttpClient client = new HttpClient();
// 
// Dictionary<string, RFMod> mods_rf = new();
// private async void RF_explore(object sender, RoutedEventArgs e)
// {
//     OpenExploer.IsEnabled = false;
//     try
//     {
//         mods_rf = await FetchModsList(client, mods_rf);
//         File.WriteAllText(jsonPath, JsonSerializer.Serialize(mods_rf, new JsonSerializerOptions { WriteIndented = true }));
//         var explorer = new exploreros(mods_rf, this);
//         explorer.Show();
//     }
//     catch (Exception ex) { }
// }