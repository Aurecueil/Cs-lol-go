import json
import urllib.request

def parse_type_obj(type_info):
    if not type_info:
        return {"type": "Unknown"}
    
    res = {}
    if "ft" in type_info and type_info["ft"] is not None:
        res["type"] = type_info["ft"]
    if "kt" in type_info and type_info["kt"] is not None:
        res["key"] = type_info["kt"]
    if "vt" in type_info and type_info["vt"] is not None:
        res["value"] = type_info["vt"]
        
    return res

def requires_rehash(source_info, target_info):
    return source_info.get("type") == "Hash" and target_info.get("type") == "File"

def requires_hash_value(type_obj):
    if type_obj.get("type") == "File":
        return True
    if type_obj.get("value") == "File":
        return True
    if type_obj.get("key") == "File":
        return True
    return False

def generate_map(changelog_url):
    req_obj = urllib.request.Request(
        changelog_url,
        headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64)"}
    )
    
    try:
        with urllib.request.urlopen(req_obj) as response:
            data = json.loads(response.read().decode('utf-8'))
    except Exception as e:
        print(f"Failed to fetch {changelog_url}: {e}")
        return []
    
    conversion_map = []
    
    for build_group in data.get("buildGroups", []):
        for entry in build_group.get("entries", []):
            class_name = entry.get("name")
            for prop in entry.get("propChanges", []):
                if prop.get("kind") != "typechanged":
                    continue
                
                field_name = prop.get("name")
                old_type_info = prop.get("oldType", {})
                new_type_info = prop.get("newType", {})
                
                source_info = parse_type_obj(old_type_info)
                target_info = parse_type_obj(new_type_info)
                
                rule = {
                    "class": class_name,
                    "field": field_name,
                    "from": source_info,
                    "to": target_info
                }
                
                if requires_rehash(source_info, target_info):
                    rule["conversion"] = "rehash"
                elif requires_hash_value(target_info) or requires_hash_value(source_info):
                    rule["conversion"] = "hash_value"
                
                conversion_map.append(rule)
                
    return conversion_map

if __name__ == "__main__":
    patches_metadata = {
        "patches": [
            {"patch": "16.20", "slug": "16-20"},
            {"patch": "16.19", "slug": "16-19"},
            {"patch": "16.18", "slug": "16-18"},
            {"patch": "16.17", "slug": "16-17"},
            {"patch": "16.16", "slug": "16-16"},
            {"patch": "16.15", "slug": "16-15"},
            {"patch": "16.14", "slug": "16-14"},
            {"patch": "16.13", "slug": "16-13"},
            {"patch": "16.12", "slug": "16-12"},
            {"patch": "16.11", "slug": "16-11"},
            {"patch": "16.10", "slug": "16-10"},
            {"patch": "16.9", "slug": "16-9"},
            {"patch": "16.8", "slug": "16-8"},
            {"patch": "16.7", "slug": "16-7"},
            {"patch": "16.6", "slug": "16-6"},
            {"patch": "16.5", "slug": "16-5"},
            {"patch": "16.4", "slug": "16-4"},
            {"patch": "16.3", "slug": "16-3"},
            {"patch": "16.2", "slug": "16-2"},
            {"patch": "16.1", "slug": "16-1"},
            {"patch": "15.24", "slug": "15-24"},
            {"patch": "15.23", "slug": "15-23"},
            {"patch": "15.22", "slug": "15-22"},
            {"patch": "15.21", "slug": "15-21"},
            {"patch": "15.20", "slug": "15-20"},
            {"patch": "15.19", "slug": "15-19"},
            {"patch": "15.18", "slug": "15-18"},
            {"patch": "15.17", "slug": "15-17"},
            {"patch": "15.16", "slug": "15-16"},
            {"patch": "15.15", "slug": "15-15"},
            {"patch": "15.14", "slug": "15-14"},
            {"patch": "15.13", "slug": "15-13"},
            {"patch": "15.12", "slug": "15-12"},
            {"patch": "15.11", "slug": "15-11"},
            {"patch": "15.10", "slug": "15-10"},
            {"patch": "15.9", "slug": "15-9"},
            {"patch": "15.8", "slug": "15-8"},
            {"patch": "15.7", "slug": "15-7"},
            {"patch": "15.6", "slug": "15-6"},
            {"patch": "15.5", "slug": "15-5"},
            {"patch": "15.4", "slug": "15-4"},
            {"patch": "15.3", "slug": "15-3"},
            {"patch": "15.2", "slug": "15-2"},
            {"patch": "15.1", "slug": "15-1"},
            {"patch": "14.24", "slug": "14-24"},
            {"patch": "14.23", "slug": "14-23"},
            {"patch": "14.22", "slug": "14-22"},
            {"patch": "14.21", "slug": "14-21"},
            {"patch": "14.20", "slug": "14-20"},
            {"patch": "14.19", "slug": "14-19"},
            {"patch": "14.18", "slug": "14-18"},
            {"patch": "14.17", "slug": "14-17"},
            {"patch": "14.16", "slug": "14-16"},
            {"patch": "14.15", "slug": "14-15"},
            {"patch": "14.14", "slug": "14-14"},
            {"patch": "14.13", "slug": "14-13"},
            {"patch": "14.12", "slug": "14-12"},
            {"patch": "14.11", "slug": "14-11"},
            {"patch": "14.10", "slug": "14-10"},
            {"patch": "14.9", "slug": "14-9"},
            {"patch": "14.8", "slug": "14-8"},
            {"patch": "14.7", "slug": "14-7"},
            {"patch": "14.6", "slug": "14-6"},
            {"patch": "14.5", "slug": "14-5"},
            {"patch": "14.4", "slug": "14-4"},
            {"patch": "14.3", "slug": "14-3"},
            {"patch": "14.2", "slug": "14-2"},
            {"patch": "14.1", "slug": "14-1"},
            {"patch": "13.24", "slug": "13-24"},
            {"patch": "13.23", "slug": "13-23"},
            {"patch": "13.22", "slug": "13-22"},
            {"patch": "13.21", "slug": "13-21"},
            {"patch": "13.20", "slug": "13-20"},
            {"patch": "13.19", "slug": "13-19"},
            {"patch": "13.18", "slug": "13-18"},
            {"patch": "13.17", "slug": "13-17"},
            {"patch": "13.16", "slug": "13-16"}
        ]
    }

for item in patches_metadata["patches"]:
        slug = item["slug"]
        url = f"https://meta-api.leaguetoolkit.dev/v1/changelog/{slug}"
        print(f"Processing patch {slug}...")

        maps = generate_map(url)
        if maps:
            major, minor = slug.split("-")
            file_prefix = f"{major}{minor.zfill(2)}"
            filename = f"{file_prefix}.jsonl"

            with open(filename, "w", encoding="utf-8") as f:
                for m in maps:
                    f.write(json.dumps(m) + "\n")
            print(f"Saved {filename} with {len(maps)} rules.")