using CsvHelper;
using CsvHelper.Configuration;
using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using MiniJsHost;
using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Collections;
using YamlDotNet.Serialization;

internal static class Program
{

    private static MiniJs _js;

    private static JsValue HostAdd(JsValue[] args, JsValue thisVal)
    {
        double a = 0;
        double b = 0;

        if (args.Length > 0 && args[0].Type == Kind.Number) a = args[0].Number;
        if (args.Length > 1 && args[1].Type == Kind.Number) b = args[1].Number;

        return JsValue.FromNumber(a + b);
    }

    #region Counter

    private static JsValue CounterCtor(JsValue[] args, JsValue thisVal)
    {
        // thisVal is an Object handle when called as constructor/method
        if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
            return JsValue.Null();

        double v = 0;
        if (args.Length > 0 && args[0].Type == Kind.Number) v = args[0].Number;

        // Wrap borrowed this-handle into JsObject (retain because wrapper owns)
        MiniJs js = _js;
        js.RetainHandle(thisVal.Handle);
        using (JsObject obj = new JsObject(js, thisVal.Handle, true))
        {
            obj.Set("x", JsValue.FromNumber(v));
        }

        return JsValue.Null();
    }

    private static JsValue CounterInc(JsValue[] args, JsValue thisVal)
    {
        if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
            return JsValue.Null();

        MiniJs js = _js;
        js.RetainHandle(thisVal.Handle);
        using (JsObject obj = new JsObject(js, thisVal.Handle, true))
        {
            JsValue cur = obj.Get("x");
            double x = 0;
            if (cur.Type == Kind.Number) x = cur.Number;
            x = x + 1;
            obj.Set("x", JsValue.FromNumber(x));
            return JsValue.FromNumber(x);
        }
    }

    #endregion

    #region FileHelper

    private static JsValue FileHelperCtor(JsValue[] args, JsValue thisVal)
    {
        if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
            return JsValue.Null();

        string path = "";
        string mode = "r";

        if (args.Length > 0 && args[0].Type == Kind.String) path = args[0].String ?? "";
        if (args.Length > 1 && args[1].Type == Kind.String) mode = args[1].String ?? "r";

        _js.RetainHandle(thisVal.Handle);
        using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
        {
            obj.Set("_path", JsValue.FromString(path));
            obj.Set("_mode", JsValue.FromString(mode));
            obj.Set("Encoding", JsValue.FromString("utf-8"));
            obj.Set("error", JsValue.Null());
        }
        return JsValue.Null();
    }

    private static JsValue FileHelperRead(JsValue[] args, JsValue thisVal)
    {
        if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
            return JsValue.Null();

        _js.RetainHandle(thisVal.Handle);
        using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
        {
            string path = obj.Get("_path").String ?? "";
            string mode = obj.Get("_mode").String ?? "r";
            string encName = obj.Get("Encoding").String ?? "utf-8";
            obj.Set("error", JsValue.Null());

            try
            {
                if (mode.Contains("b"))
                {
                    byte[] data = File.ReadAllBytes(path);
                    return JsValue.FromString(Convert.ToBase64String(data));
                }
                else
                {
                    Encoding enc = Encoding.GetEncoding(encName);
                    string text = File.ReadAllText(path, enc);
                    return JsValue.FromString(text);
                }
            }
            catch (Exception ex)
            {
                obj.Set("error", JsValue.FromString(ex.Message));
                return JsValue.Null();
            }
        }
    }

    private static JsValue FileHelperWrite(JsValue[] args, JsValue thisVal)
    {
        if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
            return JsValue.Null();

        _js.RetainHandle(thisVal.Handle);
        using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
        {
            string path = obj.Get("_path").String ?? "";
            string mode = obj.Get("_mode").String ?? "w";
            string encName = obj.Get("Encoding").String ?? "utf-8";
            obj.Set("error", JsValue.Null());

            if (args.Length < 1) return JsValue.Null();

            try
            {
                if (mode.Contains("b"))
                {
                    string b64 = args[0].String ?? "";
                    byte[] data = Convert.FromBase64String(b64);
                    FileMode fm = mode switch
                    {
                        "wb" => FileMode.Create,
                        "ab" => FileMode.Append,
                        _ => FileMode.Create
                    };
                    using (var fs = new FileStream(path, fm, FileAccess.Write, FileShare.Read))
                        fs.Write(data, 0, data.Length);
                }
                else
                {
                    string content = args[0].String ?? "";
                    Encoding enc = Encoding.GetEncoding(encName);
                    FileMode fm = mode switch
                    {
                        "w" => FileMode.Create,
                        "a" => FileMode.Append,
                        _ => FileMode.Create
                    };
                    using (var sw = new StreamWriter(new FileStream(path, fm, FileAccess.Write, FileShare.Read), enc))
                        sw.Write(content);
                }
            }
            catch (Exception ex)
            {
                obj.Set("error", JsValue.FromString(ex.Message));
            }
        }
        return JsValue.Null();
    }

    private static JsValue FileHelperExists(JsValue[] args, JsValue thisVal)
    {
        if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
            return JsValue.FromNumber(0);

        _js.RetainHandle(thisVal.Handle);
        using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
        {
            string path = obj.Get("_path").String ?? "";
            return JsValue.FromNumber(File.Exists(path) ? 1 : 0);
        }
    }

    private static JsValue FileHelperDelete(JsValue[] args, JsValue thisVal)
    {
        if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
            return JsValue.FromNumber(0);

        _js.RetainHandle(thisVal.Handle);
        using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
        {
            string path = obj.Get("_path").String ?? "";
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return JsValue.FromNumber(1);
            }
            catch (Exception ex)
            {
                obj.Set("error", JsValue.FromString(ex.Message));
                return JsValue.FromNumber(0);
            }
        }
    }

    #endregion

    #region HTML Helpers

    private static int _htmlNextId = 1;

    private sealed class HtmlState
    {
        public HtmlDocument Doc = new HtmlDocument();
        public string Html = "";
    }

    private static readonly Dictionary<int, HtmlState> _html = new();

    private static void HtmlLoad(HtmlState st, string html)
    {
        st.Doc = new HtmlDocument
        {
            OptionFixNestedTags = true,
            OptionAutoCloseOnEnd = true
        };

        st.Doc.LoadHtml(html ?? "");
        // normalisiert evtl. minimal -> ist ok
        st.Html = st.Doc.DocumentNode.OuterHtml ?? (html ?? "");
    }

    private static int HtmlCount(HtmlState st, string selector)
    {
        int i = 0;
        foreach (var _ in st.Doc.DocumentNode.QuerySelectorAll(selector)) i++;
        return i;
    }

    private static HtmlNode? HtmlSelectFirst(HtmlState st, string selector)
    {
        return st.Doc.DocumentNode.QuerySelector(selector);
    }

    private static HtmlNode? HtmlSelectAt(HtmlState st, string selector, int idx)
    {
        if (idx < 0) return null;
        int i = 0;
        foreach (var n in st.Doc.DocumentNode.QuerySelectorAll(selector))
        {
            if (i == idx) return n;
            i++;
        }
        return null;
    }

    private static void HtmlSyncToJs(JsObject obj, HtmlState st)
    {
        st.Html = st.Doc.DocumentNode.OuterHtml ?? "";
        obj.Set("_html", JsValue.FromString(st.Html));
    }

    #endregion

    #region CSV Helpers

    // static/global irgendwo:
    static int _csvNextId = 1;

    sealed class CsvTable
    {
        public List<string> Headers = new();
        public List<List<string>> Rows = new();
    }

    static readonly Dictionary<int, CsvTable> _csv = new();

    static CsvConfiguration MakeCfg(char sep, bool hasHeader)
    {
        return new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = sep.ToString(),
            HasHeaderRecord = hasHeader,

            // robust defaults
            BadDataFound = null,
            MissingFieldFound = null,
            HeaderValidated = null,
            DetectDelimiter = false,
            IgnoreBlankLines = false
        };
    }

    #endregion

    #region XML Helpers

    // irgendwo im Program:
    private static int _xmlNextId = 1;

    private sealed class XmlState
    {
        public XmlDocument Doc = new XmlDocument();
        public XmlNamespaceManager Ns;

        public XmlState()
        {
            Ns = new XmlNamespaceManager(Doc.NameTable);
            Doc.PreserveWhitespace = true;
            Doc.LoadXml("<root/>");
        }
    }

    private static readonly Dictionary<int, XmlState> _xml = new();

    #endregion

    #region JSON Helpers

    private static int _jsonNextId = 1;

    private sealed class JsonState
    {
        public JsonNode Root = new JsonObject();
    }

    private static readonly Dictionary<int, JsonState> _json = new();

    private static bool TryParsePath(string path, out List<object> segs, out string err)
    {
        segs = new List<object>();
        err = "";

        if (string.IsNullOrWhiteSpace(path))
        {
            err = "Empty path.";
            return false;
        }

        path = path.Trim();

        // JSON Pointer: /a/b/0
        if (path.StartsWith("/"))
        {
            var parts = path.Split('/', StringSplitOptions.None);
            for (int i = 1; i < parts.Length; i++)
            {
                string p = parts[i].Replace("~1", "/").Replace("~0", "~");
                if (p.Length == 0) continue;

                if (int.TryParse(p, out int idx)) segs.Add(idx);
                else segs.Add(p);
            }
            return true;
        }

        // dot + [index]: a.b[0].c
        int pos = 0;
        while (pos < path.Length)
        {
            if (path[pos] == '.')
            {
                pos++;
                continue;
            }

            // name
            int start = pos;
            while (pos < path.Length && path[pos] != '.' && path[pos] != '[')
                pos++;

            if (pos > start)
            {
                string name = path.Substring(start, pos - start).Trim();
                if (name.Length > 0)
                    segs.Add(name);
            }

            // [index] blocks
            while (pos < path.Length && path[pos] == '[')
            {
                pos++; // skip '['
                int s2 = pos;
                while (pos < path.Length && path[pos] != ']') pos++;
                if (pos >= path.Length)
                {
                    err = "Unclosed [ in path.";
                    return false;
                }

                string inside = path.Substring(s2, pos - s2).Trim();
                pos++; // skip ']'

                if (!int.TryParse(inside, out int idx))
                {
                    err = "Only numeric [index] supported in dot-path. Use JSON Pointer for complex keys.";
                    return false;
                }
                segs.Add(idx);
            }
        }

        if (segs.Count == 0)
        {
            err = "Invalid path.";
            return false;
        }
        return true;
    }

    private static bool EnsureRootType(JsonState st, object firstSeg, out string err)
    {
        err = "";
        bool wantArray = firstSeg is int;
        bool isArray = st.Root is JsonArray;
        bool isObj = st.Root is JsonObject;

        if (wantArray && !isArray)
        {
            if (isObj && (st.Root as JsonObject)!.Count == 0) { st.Root = new JsonArray(); return true; }
            err = "Root is not an array.";
            return false;
        }
        if (!wantArray && !isObj)
        {
            if (isArray && (st.Root as JsonArray)!.Count == 0) { st.Root = new JsonObject(); return true; }
            err = "Root is not an object.";
            return false;
        }
        return true;
    }

    private static JsonNode? GetNode(JsonNode root, List<object> segs)
    {
        JsonNode? cur = root;

        foreach (var seg in segs)
        {
            if (cur == null) return null;

            if (seg is string key)
            {
                if (cur is not JsonObject jo) return null;
                if (!jo.TryGetPropertyValue(key, out var next)) return null;
                cur = next;
            }
            else
            {
                int idx = (int)seg;
                if (cur is not JsonArray ja) return null;
                if (idx < 0 || idx >= ja.Count) return null;
                cur = ja[idx];
            }
        }

        return cur;
    }

    private static bool GetParent(JsonState st, List<object> segs, bool create, out JsonNode? parent, out object last, out string err)
    {
        parent = null;
        last = segs[^1];
        err = "";

        if (segs.Count == 1)
        {
            parent = st.Root;
            return true;
        }

        if (!EnsureRootType(st, segs[0], out err))
            return false;

        JsonNode? cur = st.Root;

        for (int i = 0; i < segs.Count - 1; i++)
        {
            var seg = segs[i];
            var nextSeg = segs[i + 1];
            bool nextWantsArray = nextSeg is int;

            if (seg is string key)
            {
                if (cur is not JsonObject jo)
                {
                    err = "Path walks through non-object.";
                    return false;
                }

                if (!jo.TryGetPropertyValue(key, out var child) || child == null)
                {
                    if (!create)
                    {
                        parent = null;
                        return true;
                    }
                    child = nextWantsArray ? new JsonArray() : new JsonObject();
                    jo[key] = child;
                }

                cur = child;
            }
            else
            {
                int idx = (int)seg;
                if (cur is not JsonArray ja)
                {
                    err = "Path walks through non-array.";
                    return false;
                }

                if (idx < 0) { err = "Negative index."; return false; }

                if (idx >= ja.Count)
                {
                    if (!create)
                    {
                        parent = null;
                        return true;
                    }
                    while (ja.Count <= idx) ja.Add(null);
                }

                var child = ja[idx];
                if (child == null)
                {
                    if (!create)
                    {
                        parent = null;
                        return true;
                    }
                    child = nextWantsArray ? new JsonArray() : new JsonObject();
                    ja[idx] = child;
                }

                cur = child;
            }
        }

        parent = cur;
        return true;
    }

    private static JsonNode? JsToJsonNode(JsValue v, bool parseJson, out string err)
    {
        err = "";

        if (v.Type == Kind.Number)
            return JsonValue.Create(v.Number);

        if (v.Type == Kind.String)
        {
            string s = v.String ?? "";
            if (parseJson)
            {
                try { return JsonNode.Parse(s); }
                catch (Exception ex) { err = ex.Message; return null; }
            }
            return JsonValue.Create(s);
        }

        // fallback: try interpret textual value
        string t = v.String ?? "";
        if (string.Equals(t, "null", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(true);
        if (string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(false);

        // last resort: string
        return JsonValue.Create(t);
    }

    private static JsValue JsonToJs(JsonNode? node)
    {
        if (node == null) return JsValue.Null();

        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<string>(out var s)) return JsValue.FromString(s ?? "");
            if (jv.TryGetValue<bool>(out var b)) return JsValue.FromNumber(b ? 1 : 0);
            if (jv.TryGetValue<int>(out var i)) return JsValue.FromNumber(i);
            if (jv.TryGetValue<long>(out var l)) return JsValue.FromNumber(l);
            if (jv.TryGetValue<double>(out var d)) return JsValue.FromNumber(d);
            return JsValue.FromString(jv.ToJsonString());
        }

        // object/array -> JSON string
        return JsValue.FromString(node.ToJsonString());
    }

    #endregion

    #region HTTP Helpers

    private static int _httpNextId = 1;

    private sealed class HttpState
    {
        public HttpResponseMessage? Resp;
        public Stream? RespStream;
        public StreamReader? RespReader;
        public bool StreamBinary;

        // Upload-“Streaming” (Buffer bis closestream -> dann senden)
        public MemoryStream? Upload;
        public Encoding UploadEnc = new UTF8Encoding(false);
        public bool UploadBinary;
    }

    private static readonly Dictionary<int, HttpState> _http = new();

    private static readonly HttpClient _httpClient = new HttpClient(new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.All,
        AllowAutoRedirect = true
    })
    {
        Timeout = Timeout.InfiniteTimeSpan
    };

    #endregion

    #region YAML Helpers 

    private static int _yamlNextId = 1;

    private sealed class YamlState
    {
        public JsonNode Root = new JsonObject();
    }

    private static readonly Dictionary<int, YamlState> _yaml = new();

    private static readonly IDeserializer _yamlDes = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    private static readonly ISerializer _yamlSer = new SerializerBuilder()
        .DisableAliases()
        .Build();

    private static JsonNode? YamlObjToJsonNode(object? o)
    {
        if (o == null) return null;

        if (o is string s) return JsonValue.Create(s);
        if (o is bool b) return JsonValue.Create(b);

        if (o is int i) return JsonValue.Create(i);
        if (o is long l) return JsonValue.Create(l);
        if (o is double d) return JsonValue.Create(d);
        if (o is float f) return JsonValue.Create((double)f);
        if (o is decimal m) return JsonValue.Create((double)m);

        if (o is DateTime dt)
            return JsonValue.Create(dt.ToString("o", CultureInfo.InvariantCulture));

        // YAML mappings: Dictionary<object, object>
        if (o is IDictionary dict)
        {
            var jo = new JsonObject();
            foreach (DictionaryEntry de in dict)
            {
                string key = de.Key?.ToString() ?? "";
                jo[key] = YamlObjToJsonNode(de.Value);
            }
            return jo;
        }

        // YAML sequences: List<object>
        if (o is IEnumerable en && o is not string)
        {
            var ja = new JsonArray();
            foreach (var item in en)
                ja.Add(YamlObjToJsonNode(item));
            return ja;
        }

        return JsonValue.Create(o.ToString());
    }

    private static object? JsonNodeToYamlObj(JsonNode? node)
    {
        if (node == null) return null;

        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<string>(out var s)) return s;
            if (jv.TryGetValue<bool>(out var b)) return b;
            if (jv.TryGetValue<int>(out var i)) return i;
            if (jv.TryGetValue<long>(out var l)) return l;
            if (jv.TryGetValue<double>(out var d)) return d;
            if (jv.TryGetValue<decimal>(out var m)) return m;

            // fallback
            return jv.ToJsonString();
        }

        if (node is JsonObject jo)
        {
            var d = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var kv in jo)
                d[kv.Key] = JsonNodeToYamlObj(kv.Value);
            return d;
        }

        if (node is JsonArray ja)
        {
            var list = new List<object?>();
            foreach (var item in ja)
                list.Add(JsonNodeToYamlObj(item));
            return list;
        }

        return node.ToJsonString();
    }

    private static JsValue YamlToJs(JsonNode? node)
    {
        if (node == null) return JsValue.Null();

        if (node is JsonValue jv)
        {
            if (jv.TryGetValue<string>(out var s)) return JsValue.FromString(s ?? "");
            if (jv.TryGetValue<bool>(out var b)) return JsValue.FromNumber(b ? 1 : 0);

            if (jv.TryGetValue<int>(out var i)) return JsValue.FromNumber(i);
            if (jv.TryGetValue<long>(out var l)) return JsValue.FromNumber(l);
            if (jv.TryGetValue<double>(out var d)) return JsValue.FromNumber(d);

            return JsValue.FromString(jv.ToJsonString());
        }

        // object/array -> YAML string
        string y = _yamlSer.Serialize(JsonNodeToYamlObj(node) ?? new object());
        return JsValue.FromString(y.TrimEnd());
    }

    private static JsonNode? JsToYamlNode(JsValue v, bool parseYaml, out string err)
    {
        err = "";

        if (v.Type == Kind.Number) return JsonValue.Create(v.Number);

        if (v.Type == Kind.String)
        {
            string s = v.String ?? "";

            if (parseYaml)
            {
                try
                {
                    var obj = _yamlDes.Deserialize<object>(s);
                    return YamlObjToJsonNode(obj);
                }
                catch (Exception ex)
                {
                    err = ex.Message;
                    return null;
                }
            }

            return JsonValue.Create(s);
        }

        // simple textual fallbacks
        string t = v.String ?? "";
        if (string.Equals(t, "null", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(true);
        if (string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)) return JsonValue.Create(false);

        return JsonValue.Create(t);
    }

    // eigene GetParent-Variante für YamlState (kopiert von deinem JSON-GetParent)
    private static bool EnsureRootType(YamlState st, object firstSeg, out string err)
    {
        err = "";
        bool wantArray = firstSeg is int;
        bool isArray = st.Root is JsonArray;
        bool isObj = st.Root is JsonObject;

        if (wantArray && !isArray)
        {
            if (isObj && (st.Root as JsonObject)!.Count == 0) { st.Root = new JsonArray(); return true; }
            err = "Root is not an array.";
            return false;
        }
        if (!wantArray && !isObj)
        {
            if (isArray && (st.Root as JsonArray)!.Count == 0) { st.Root = new JsonObject(); return true; }
            err = "Root is not an object.";
            return false;
        }
        return true;
    }

    private static bool GetParent(YamlState st, List<object> segs, bool create, out JsonNode? parent, out object last, out string err)
    {
        parent = null;
        last = segs[^1];
        err = "";

        if (segs.Count == 1)
        {
            parent = st.Root;
            return true;
        }

        if (!EnsureRootType(st, segs[0], out err))
            return false;

        JsonNode? cur = st.Root;

        for (int i = 0; i < segs.Count - 1; i++)
        {
            var seg = segs[i];
            var nextSeg = segs[i + 1];
            bool nextWantsArray = nextSeg is int;

            if (seg is string key)
            {
                if (cur is not JsonObject jo)
                {
                    err = "Path walks through non-object.";
                    return false;
                }

                if (!jo.TryGetPropertyValue(key, out var child) || child == null)
                {
                    if (!create) { parent = null; return true; }
                    child = nextWantsArray ? new JsonArray() : new JsonObject();
                    jo[key] = child;
                }

                cur = child;
            }
            else
            {
                int idx = (int)seg;
                if (cur is not JsonArray ja)
                {
                    err = "Path walks through non-array.";
                    return false;
                }

                if (idx < 0) { err = "Negative index."; return false; }

                if (idx >= ja.Count)
                {
                    if (!create) { parent = null; return true; }
                    while (ja.Count <= idx) ja.Add(null);
                }

                var child = ja[idx];
                if (child == null)
                {
                    if (!create) { parent = null; return true; }
                    child = nextWantsArray ? new JsonArray() : new JsonObject();
                    ja[idx] = child;
                }

                cur = child;
            }
        }

        parent = cur;
        return true;
    }

    #endregion

    public static int Main(string[] args)
    {

        if (args.Length < 1)
        {
            Console.WriteLine("Usage: MiniJS_Demo.exe <scriptfile>");
            return 1;
        }

        _js = new MiniJs();

        #region Prototypes

        _js.Register("hostAdd", HostAdd);

        using (JsClass counter = _js.CreateClass("Counter"))
        {
            counter.AddMethod("constructor", CounterCtor);
            counter.AddMethod("inc", CounterInc);
            counter.DeclareToGlobals(); // puts Counter into JS globals
        }

        using (JsArray arr = _js.CreateArray())
        {
            arr.Push(JsValue.FromNumber(1));
            arr.Push(JsValue.FromNumber(2));
            arr.Push(JsValue.FromString("hi"));
            _js.Declare("hostArr", arr); // transfers ownership to JS runtime
        }

        using (JsObject obj = _js.CreateObject())
        {
            obj.Set("a", JsValue.FromNumber(123));
            obj.Set("b", JsValue.FromString("text"));
            _js.Declare("hostObj", obj); // transfers ownership to JS runtime
        }

        using (JsClass console = _js.CreateClass("Console"))
        {
            console.AddMethod("constructor", (JsValue[] args, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.Null();
                MiniJs js = _js;
                js.RetainHandle(thisVal.Handle);
                return JsValue.Null();
            });
            console.AddMethod("print", (JsValue[] args, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);

                var sb = new StringBuilder();

                foreach (var arg in args)
                {
                    switch (arg.Type)
                    {
                        case Kind.String:
                            sb.Append(arg.String ?? "");
                            break;

                        case Kind.Number:
                            sb.Append(arg.Number.ToString(CultureInfo.InvariantCulture));
                            break;

                        case Kind.Null:
                            sb.Append("null");
                            break;

                        default:
                            // fallback: wenn du für Object/Bool/etc. etwas besseres hast, hier rein
                            sb.Append(arg.String ?? "");
                            break;
                    }
                }

                Console.Write(sb.ToString());
                return JsValue.Null();
            });
            console.AddMethod("read", (JsValue[] args, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);

                var line = Console.ReadLine() ?? "";
                return JsValue.FromString(line);
            });
            console.DeclareToGlobals(); // puts Counter into JS globals
        }

        using (JsClass filehelper = _js.CreateClass("FileHelper"))
        {
            filehelper.AddMethod("constructor", FileHelperCtor);
            filehelper.AddMethod("read", FileHelperRead);
            filehelper.AddMethod("write", FileHelperWrite);
            filehelper.AddMethod("exists", FileHelperExists);
            filehelper.AddMethod("delete", FileHelperDelete);
            filehelper.DeclareToGlobals(); // puts Counter into JS globals
        }

        using (JsClass file = _js.CreateClass("File"))
        {
            // -------- Encoding Resolver (lokal, minimal) --------
            Func<string?, Encoding> ResolveEncoding = (encRaw) =>
            {
                string e = (encRaw ?? "utf-8").Trim().ToLowerInvariant();
                return e switch
                {
                    "utf-8" or "utf8" or "utf-8-nobom" or "utf8-nobom" => new UTF8Encoding(false),
                    "utf-8-bom" or "utf8-bom" => new UTF8Encoding(true),
                    "utf-16" or "utf16" or "utf-16le" or "utf16le" or "unicode" => new UnicodeEncoding(false, false),
                    "utf-16-bom" or "utf16-bom" or "utf-16le-bom" or "utf16le-bom" or "unicode-bom" => new UnicodeEncoding(false, true),
                    "utf-16be" or "utf16be" => new UnicodeEncoding(true, false),
                    "utf-16be-bom" or "utf16be-bom" => new UnicodeEncoding(true, true),
                    "utf-32" or "utf32" or "utf-32le" or "utf32le" => new UTF32Encoding(false, false),
                    "utf-32-bom" or "utf32-bom" or "utf-32le-bom" or "utf32le-bom" => new UTF32Encoding(false, true),
                    "utf-32be" or "utf32be" => new UTF32Encoding(true, false),
                    "utf-32be-bom" or "utf32be-bom" => new UTF32Encoding(true, true),
                    _ => Encoding.GetEncoding(encRaw ?? "utf-8")
                };
            };

            // -------- Bytearray Converter (Base64 / JS Array) --------
            Func<JsValue, byte[]> GetBytes = (v) =>
            {
                if (v.Type == Kind.String)
                    return Convert.FromBase64String(v.String ?? "");

                if (v.Type == Kind.Object && v.Handle != IntPtr.Zero)
                {
                    _js.RetainHandle(v.Handle);
                    using var arr = new JsObject(_js, v.Handle, true);

                    int len = (arr.Get("length").Type == Kind.Number) ? (int)arr.Get("length").Number : 0;
                    byte[] data = new byte[len];
                    for (int i = 0; i < len; i++)
                    {
                        JsValue vi = arr.Get(i.ToString());
                        int b = (vi.Type == Kind.Number) ? (int)vi.Number : 0;
                        data[i] = (byte)Math.Clamp(b, 0, 255);
                    }
                    return data;
                }
                throw new Exception("Binary write expects Base64 string or byte array [0..255].");
            };

            file.AddMethod("constructor", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.Null();

                string path = (a.Length > 0 && a[0].Type == Kind.String) ? a[0].String ?? "" : "";
                string mode = (a.Length > 1 && a[1].Type == Kind.String) ? a[1].String ?? "r" : "r";

                _js.RetainHandle(thisVal.Handle);
                using JsObject obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("_path", JsValue.FromString(path));
                obj.Set("_mode", JsValue.FromString(mode));
                obj.Set("Encoding", JsValue.FromString("utf-8"));
                obj.Set("error", JsValue.Null());
                return JsValue.Null();
            });

            FileMode GetFileMode(string mode)
            {
                bool plus = mode.Contains('+');
                bool append = mode.StartsWith('a');
                bool write = mode.StartsWith('w');

                if (append) return FileMode.Append;
                if (write) return FileMode.Create;
                if (plus && System.IO.File.Exists(mode)) return FileMode.OpenOrCreate;
                return FileMode.Open;
            }

            file.AddMethod("read", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using JsObject obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                string path = obj.Get("_path").String ?? "";
                string mode = (obj.Get("_mode").String ?? "r").Trim().ToLowerInvariant();

                if (!mode.Contains('r') && !mode.Contains('+'))
                {
                    obj.Set("error", JsValue.FromString($"Mode '{mode}' not readable."));
                    return JsValue.Null();
                }

                bool bin = mode.Contains('b');
                try
                {
                    if (bin)
                    {
                        byte[] data = File.ReadAllBytes(path);
                        return JsValue.FromString(Convert.ToBase64String(data));
                    }
                    else
                    {
                        string encName = obj.Get("Encoding").String ?? "utf-8";
                        Encoding enc = ResolveEncoding(encName);
                        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        using var sr = new StreamReader(fs, enc, detectEncodingFromByteOrderMarks: true);
                        return JsValue.FromString(sr.ReadToEnd());
                    }
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            file.AddMethod("write", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using JsObject obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                string path = obj.Get("_path").String ?? "";
                string mode = (obj.Get("_mode").String ?? "w").Trim().ToLowerInvariant();

                if (!mode.Contains('w') && !mode.Contains('a') && !mode.Contains('+'))
                {
                    obj.Set("error", JsValue.FromString($"Mode '{mode}' not writable."));
                    return JsValue.FromNumber(0);
                }
                if (a.Length < 1)
                {
                    obj.Set("error", JsValue.FromString("Missing data."));
                    return JsValue.FromNumber(0);
                }

                bool bin = mode.Contains('b');
                bool append = mode.StartsWith('a');
                bool truncate = mode.StartsWith('w');
                FileMode fm = append ? FileMode.Append : (truncate ? FileMode.Create : FileMode.OpenOrCreate);

                try
                {
                    if (bin)
                    {
                        byte[] data = GetBytes(a[0]);
                        using var fs = new FileStream(path, fm, FileAccess.Write, FileShare.Read);
                        fs.Write(data, 0, data.Length);
                    }
                    else
                    {
                        string content = a[0].String ?? "";
                        string encName = obj.Get("Encoding").String ?? "utf-8";
                        Encoding enc = ResolveEncoding(encName);
                        using var fs = new FileStream(path, fm, FileAccess.Write, FileShare.Read);
                        using var sw = new StreamWriter(fs, enc);
                        sw.Write(content);
                    }
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            file.AddMethod("exists", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using JsObject obj = new JsObject(_js, thisVal.Handle, true);
                string path = obj.Get("_path").String ?? "";
                return JsValue.FromNumber(File.Exists(path) ? 1 : 0);
            });

            file.AddMethod("delete", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using JsObject obj = new JsObject(_js, thisVal.Handle, true);
                string path = obj.Get("_path").String ?? "";
                try
                {
                    if (File.Exists(path)) File.Delete(path);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            file.DeclareToGlobals();
        }

        using (JsClass html = _js.CreateClass("HTML"))
        {
            // new HTML([htmlString])
            html.AddMethod("constructor", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                string s = (a.Length > 0 && a[0].Type == Kind.String) ? (a[0].String ?? "") : "";

                int id = _htmlNextId++;
                var st = new HtmlState();
                HtmlLoad(st, s);
                _html[id] = st;

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("_id", JsValue.FromNumber(id));
                    obj.Set("_html", JsValue.FromString(st.Html));
                    obj.Set("error", JsValue.Null());
                }

                return JsValue.Null();
            });

            // free() -> 1/0
            html.AddMethod("free", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0) return JsValue.FromNumber(0);

                return JsValue.FromNumber(_html.Remove(id) ? 1 : 0);
            });

            // setHtml(string) -> 1/0
            html.AddMethod("setHtml", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                string s = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    HtmlLoad(st, s);
                    obj.Set("_html", JsValue.FromString(st.Html));
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // html() -> whole html
            html.AddMethod("html", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st)) return JsValue.Null();

                return JsValue.FromString(st.Html ?? "");
            });

            // count(selector) -> number
            html.AddMethod("count", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    return JsValue.FromNumber(HtmlCount(st, sel));
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // outerHTML(selector) -> first match
            html.AddMethod("outerHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    return node == null ? JsValue.Null() : JsValue.FromString(node.OuterHtml ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // outerHTMLAt(selector, index) -> nth match
            html.AddMethod("outerHTMLAt", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                int idx = (a.Length > 1 && a[1].Type == Kind.Number) ? (int)a[1].Number : 0;

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var node = HtmlSelectAt(st, sel, idx);
                    return node == null ? JsValue.Null() : JsValue.FromString(node.OuterHtml ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // innerHTML(selector)
            html.AddMethod("innerHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    return node == null ? JsValue.Null() : JsValue.FromString(node.InnerHtml ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // innerHTMLAt(selector, index)
            html.AddMethod("innerHTMLAt", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                int idx = (a.Length > 1 && a[1].Type == Kind.Number) ? (int)a[1].Number : 0;

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var node = HtmlSelectAt(st, sel, idx);
                    return node == null ? JsValue.Null() : JsValue.FromString(node.InnerHtml ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // innerText(selector)
            html.AddMethod("innerText", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    return node == null ? JsValue.Null() : JsValue.FromString(node.InnerText ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // innerTextAt(selector, index)
            html.AddMethod("innerTextAt", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                int idx = (a.Length > 1 && a[1].Type == Kind.Number) ? (int)a[1].Number : 0;

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var node = HtmlSelectAt(st, sel, idx);
                    return node == null ? JsValue.Null() : JsValue.FromString(node.InnerText ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // getAttr(selector, name)
            html.AddMethod("getAttr", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    if (node == null) return JsValue.Null();

                    var attr = node.Attributes[name];
                    return attr == null ? JsValue.Null() : JsValue.FromString(attr.Value ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // getAttrAt(selector, index, name)
            html.AddMethod("getAttrAt", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                int idx = (a.Length > 1 && a[1].Type == Kind.Number) ? (int)a[1].Number : 0;
                string name = (a.Length > 2) ? (a[2].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var node = HtmlSelectAt(st, sel, idx);
                    if (node == null) return JsValue.Null();

                    var attr = node.Attributes[name];
                    return attr == null ? JsValue.Null() : JsValue.FromString(attr.Value ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // setAttr(selector, name, value) -> 1/0
            html.AddMethod("setAttr", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";
                string value = (a.Length > 2) ? (a[2].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    if (node == null)
                    {
                        obj.Set("error", JsValue.FromString("Selector not found."));
                        return JsValue.FromNumber(0);
                    }

                    node.SetAttributeValue(name, value);
                    HtmlSyncToJs(obj, st);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // setAttrAt(selector, index, name, value) -> 1/0
            html.AddMethod("setAttrAt", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                int idx = (a.Length > 1 && a[1].Type == Kind.Number) ? (int)a[1].Number : 0;
                string name = (a.Length > 2) ? (a[2].String ?? "") : "";
                string value = (a.Length > 3) ? (a[3].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var node = HtmlSelectAt(st, sel, idx);
                    if (node == null)
                    {
                        obj.Set("error", JsValue.FromString("Selector/index not found."));
                        return JsValue.FromNumber(0);
                    }

                    node.SetAttributeValue(name, value);
                    HtmlSyncToJs(obj, st);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // setInnerHTML(selector, htmlFragment) -> 1/0
            html.AddMethod("setInnerHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string frag = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    if (node == null)
                    {
                        obj.Set("error", JsValue.FromString("Selector not found."));
                        return JsValue.FromNumber(0);
                    }

                    node.InnerHtml = frag;
                    HtmlSyncToJs(obj, st);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // setInnerText(selector, text) -> 1/0 (escaped)
            html.AddMethod("setInnerText", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string txt = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    if (node == null)
                    {
                        obj.Set("error", JsValue.FromString("Selector not found."));
                        return JsValue.FromNumber(0);
                    }

                    node.InnerHtml = HtmlEntity.Entitize(txt);
                    HtmlSyncToJs(obj, st);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // appendHTML(selector, htmlFragment) -> 1/0
            html.AddMethod("appendHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string frag = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    if (node == null)
                    {
                        obj.Set("error", JsValue.FromString("Selector not found."));
                        return JsValue.FromNumber(0);
                    }

                    node.InnerHtml = (node.InnerHtml ?? "") + frag;
                    HtmlSyncToJs(obj, st);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // prependHTML(selector, htmlFragment) -> 1/0
            html.AddMethod("prependHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string frag = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    if (node == null)
                    {
                        obj.Set("error", JsValue.FromString("Selector not found."));
                        return JsValue.FromNumber(0);
                    }

                    node.InnerHtml = frag + (node.InnerHtml ?? "");
                    HtmlSyncToJs(obj, st);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // remove(selector) -> 1/0 (first match)
            html.AddMethod("remove", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var node = HtmlSelectFirst(st, sel);
                    if (node == null) return JsValue.FromNumber(0);

                    node.Remove();
                    HtmlSyncToJs(obj, st);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // removeAt(selector, index) -> 1/0
            html.AddMethod("removeAt", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                int idx = (a.Length > 1 && a[1].Type == Kind.Number) ? (int)a[1].Number : 0;

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_html.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var node = HtmlSelectAt(st, sel, idx);
                    if (node == null) return JsValue.FromNumber(0);

                    node.Remove();
                    HtmlSyncToJs(obj, st);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            html.DeclareToGlobals();
        }


        using (JsClass csv = _js.CreateClass("CSV")) // Script: new CSV()
        {
            // new CSV()
            csv.AddMethod("constructor", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.Null();

                int id = _csvNextId++;
                _csv[id] = new CsvTable();

                _js.RetainHandle(thisVal.Handle);
                using (var obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("_id", JsValue.FromNumber(id));
                    obj.Set("error", JsValue.Null());

                    obj.Set("ReadSep", JsValue.FromString(","));
                    obj.Set("WriteSep", JsValue.FromString(","));
                    obj.Set("HasHeader", JsValue.FromNumber(1));
                    obj.Set("WriteHeader", JsValue.FromNumber(1));
                }
                return JsValue.Null();
            });

            // parse(text) -> 1/0
            csv.AddMethod("parse", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using (var obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());

                    int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                    if (id < 0 || !_csv.TryGetValue(id, out var t))
                    {
                        obj.Set("error", JsValue.FromString("CSV state missing."));
                        return JsValue.FromNumber(0);
                    }

                    string text = (a.Length > 0) ? (a[0].String ?? "") : "";

                    string rs = obj.Get("ReadSep").String ?? ",";
                    char readSep = rs.Length > 0 ? rs[0] : ',';

                    bool hasHeader = (obj.Get("HasHeader").Type == Kind.Number) && ((int)obj.Get("HasHeader").Number != 0);

                    try
                    {
                        t.Headers.Clear();
                        t.Rows.Clear();

                        using var sr = new StringReader(text);
                        using var cr = new CsvReader(sr, MakeCfg(readSep, hasHeader));

                        if (hasHeader)
                        {
                            if (cr.Read())
                            {
                                cr.ReadHeader();
                                if (cr.HeaderRecord != null)
                                    t.Headers.AddRange(cr.HeaderRecord.Select(x => x ?? ""));
                            }
                        }

                        while (cr.Read())
                        {
                            // CsvHelper re-used arrays -> kopieren!
                            var rec = cr.Parser.Record;
                            t.Rows.Add(rec?.Select(x => x ?? "").ToList() ?? new List<string>());
                        }

                        return JsValue.FromNumber(1);
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.FromNumber(0);
                    }
                }
            });

            // toCsv([writeHeader 0/1]) -> string|null
            csv.AddMethod("toCsv", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using (var obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());

                    int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                    if (id < 0 || !_csv.TryGetValue(id, out var t))
                    {
                        obj.Set("error", JsValue.FromString("CSV state missing."));
                        return JsValue.Null();
                    }

                    string ws = obj.Get("WriteSep").String ?? ",";
                    char writeSep = ws.Length > 0 ? ws[0] : ',';

                    bool writeHeader;
                    if (a.Length > 0 && a[0].Type == Kind.Number) writeHeader = ((int)a[0].Number != 0);
                    else writeHeader = (obj.Get("WriteHeader").Type == Kind.Number) && ((int)obj.Get("WriteHeader").Number != 0);

                    try
                    {
                        int cols = t.Headers.Count;
                        for (int r = 0; r < t.Rows.Count; r++)
                            if (t.Rows[r].Count > cols) cols = t.Rows[r].Count;

                        using var sw = new StringWriter();
                        using var cw = new CsvWriter(sw, MakeCfg(writeSep, writeHeader));

                        if (writeHeader && cols > 0)
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                string h = (c < t.Headers.Count) ? (t.Headers[c] ?? "") : "";
                                cw.WriteField(h);
                            }
                            cw.NextRecord();
                        }

                        for (int r = 0; r < t.Rows.Count; r++)
                        {
                            var row = t.Rows[r];
                            for (int c = 0; c < cols; c++)
                            {
                                string v = (c < row.Count) ? (row[c] ?? "") : "";
                                cw.WriteField(v);
                            }
                            cw.NextRecord();
                        }

                        return JsValue.FromString(sw.ToString());
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.Null();
                    }
                }
            });

            // rowCount()
            csv.AddMethod("rowCount", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                return (id >= 0 && _csv.TryGetValue(id, out var t)) ? JsValue.FromNumber(t.Rows.Count) : JsValue.FromNumber(0);
            });

            // colCount()
            csv.AddMethod("colCount", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.FromNumber(0);

                int cols = t.Headers.Count;
                for (int r = 0; r < t.Rows.Count; r++)
                    if (t.Rows[r].Count > cols) cols = t.Rows[r].Count;

                return JsValue.FromNumber(cols);
            });

            // get(r,c) -> string|null
            csv.AddMethod("get", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                int r = (a.Length > 0 && a[0].Type == Kind.Number) ? (int)a[0].Number : -1;
                int c = (a.Length > 1 && a[1].Type == Kind.Number) ? (int)a[1].Number : -1;

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.Null();

                if (r < 0 || c < 0 || r >= t.Rows.Count) return JsValue.Null();
                if (c >= t.Rows[r].Count) return JsValue.FromString("");
                return JsValue.FromString(t.Rows[r][c] ?? "");
            });

            // set(r,c,val) -> 1/0
            csv.AddMethod("set", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                int r = (a.Length > 0 && a[0].Type == Kind.Number) ? (int)a[0].Number : -1;
                int c = (a.Length > 1 && a[1].Type == Kind.Number) ? (int)a[1].Number : -1;
                string v = (a.Length > 2) ? (a[2].String ?? "") : "";

                if (r < 0 || c < 0) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.FromNumber(0);

                while (t.Rows.Count <= r) t.Rows.Add(new List<string>());
                while (t.Rows[r].Count <= c) t.Rows[r].Add("");

                t.Rows[r][c] = v ?? "";
                return JsValue.FromNumber(1);
            });

            // header(i) -> string|null
            csv.AddMethod("header", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                int i = (a.Length > 0 && a[0].Type == Kind.Number) ? (int)a[0].Number : -1;

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.Null();

                if (i < 0 || i >= t.Headers.Count) return JsValue.Null();
                return JsValue.FromString(t.Headers[i] ?? "");
            });

            // setHeader(i, name) -> 1/0
            csv.AddMethod("setHeader", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                int i = (a.Length > 0 && a[0].Type == Kind.Number) ? (int)a[0].Number : -1;
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";
                if (i < 0) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.FromNumber(0);

                while (t.Headers.Count <= i) t.Headers.Add("");
                t.Headers[i] = name ?? "";
                return JsValue.FromNumber(1);
            });

            // colIndex(name) -> index or -1
            csv.AddMethod("colIndex", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(-1);
                string name = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.FromNumber(-1);

                for (int i = 0; i < t.Headers.Count; i++)
                    if (string.Equals(t.Headers[i] ?? "", name, StringComparison.OrdinalIgnoreCase))
                        return JsValue.FromNumber(i);

                return JsValue.FromNumber(-1);
            });

            // getByName(row, name) -> string|null
            csv.AddMethod("getByName", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                int r = (a.Length > 0 && a[0].Type == Kind.Number) ? (int)a[0].Number : -1;
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.Null();

                int col = -1;
                for (int i = 0; i < t.Headers.Count; i++)
                    if (string.Equals(t.Headers[i] ?? "", name, StringComparison.OrdinalIgnoreCase)) { col = i; break; }

                if (col < 0 || r < 0 || r >= t.Rows.Count) return JsValue.Null();
                if (col >= t.Rows[r].Count) return JsValue.FromString("");
                return JsValue.FromString(t.Rows[r][col] ?? "");
            });

            // setByName(row, name, value) -> 1/0  (legt Spalte an, wenn nicht vorhanden)
            csv.AddMethod("setByName", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                int r = (a.Length > 0 && a[0].Type == Kind.Number) ? (int)a[0].Number : -1;
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";
                string val = (a.Length > 2) ? (a[2].String ?? "") : "";
                if (r < 0) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.FromNumber(0);

                int col = -1;
                for (int i = 0; i < t.Headers.Count; i++)
                    if (string.Equals(t.Headers[i] ?? "", name, StringComparison.OrdinalIgnoreCase)) { col = i; break; }

                if (col < 0)
                {
                    t.Headers.Add(name ?? "");
                    col = t.Headers.Count - 1;
                }

                while (t.Rows.Count <= r) t.Rows.Add(new List<string>());
                while (t.Rows[r].Count <= col) t.Rows[r].Add("");

                t.Rows[r][col] = val ?? "";
                return JsValue.FromNumber(1);
            });

            // addRow() -> new row index
            csv.AddMethod("addRow", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(-1);
                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.FromNumber(-1);

                t.Rows.Add(new List<string>());
                return JsValue.FromNumber(t.Rows.Count - 1);
            });

            // addColumn(name?) -> new col index
            csv.AddMethod("addColumn", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(-1);
                string name = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_csv.TryGetValue(id, out var t)) return JsValue.FromNumber(-1);

                t.Headers.Add(name ?? "");
                int col = t.Headers.Count - 1;

                for (int r = 0; r < t.Rows.Count; r++)
                    while (t.Rows[r].Count <= col) t.Rows[r].Add("");

                return JsValue.FromNumber(col);
            });

            // free() -> 1/0
            csv.AddMethod("free", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                return JsValue.FromNumber((id >= 0 && _csv.Remove(id)) ? 1 : 0);
            });

            csv.DeclareToGlobals();

        }

        using (JsClass xml = _js.CreateClass("XML")) // Script: new XML()
        {

            xml.AddMethod("constructor", (JsValue[] a, JsValue thisVal) =>
            {

                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                int id = _xmlNextId++;
                var st = new XmlState();
                _xml[id] = st;

                _js.RetainHandle(thisVal.Handle);
                using (var obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("_id", JsValue.FromNumber(id));
                    obj.Set("error", JsValue.Null());

                    if (a.Length > 0 && a[0].Type == Kind.String)
                    {
                        string s = a[0].String ?? "";
                        try
                        {
                            st.Doc = new XmlDocument { PreserveWhitespace = true };
                            st.Ns = new XmlNamespaceManager(st.Doc.NameTable);
                            st.Doc.LoadXml(s);
                            _xml[id] = st; // update state
                        }
                        catch (Exception ex)
                        {
                            obj.Set("error", JsValue.FromString(ex.Message));
                        }
                    }
                }

                return JsValue.Null();
            });

            xml.AddMethod("free", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                return JsValue.FromNumber((id >= 0 && _xml.Remove(id)) ? 1 : 0);
            });

            xml.AddMethod("setXml", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                string s = (a.Length > 0) ? (a[0].String ?? "") : "";
                try
                {
                    st.Doc = new XmlDocument { PreserveWhitespace = true };
                    st.Ns = new XmlNamespaceManager(st.Doc.NameTable);
                    st.Doc.LoadXml(s);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("xml", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st)) return JsValue.Null();

                return JsValue.FromString(st.Doc.OuterXml ?? "");
            });

            xml.AddMethod("addNamespace", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                string prefix = (a.Length > 0) ? (a[0].String ?? "") : "";
                string uri = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    st.Ns.AddNamespace(prefix, uri);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("exists", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st)) return JsValue.FromNumber(0);

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    return JsValue.FromNumber(n != null ? 1 : 0);
                }
                catch
                {
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("count", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st)) return JsValue.FromNumber(0);

                try
                {
                    var list = st.Doc.SelectNodes(xp, st.Ns);
                    return JsValue.FromNumber(list?.Count ?? 0);
                }
                catch
                {
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("innerText", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    if (n == null) return JsValue.Null();
                    return JsValue.FromString(n.InnerText ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            xml.AddMethod("innerXML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    if (n == null) return JsValue.Null();
                    return JsValue.FromString(n.InnerXml ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            xml.AddMethod("outerXML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    if (n == null) return JsValue.Null();
                    return JsValue.FromString(n.OuterXml ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            xml.AddMethod("setInnerText", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";
                string txt = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    if (n == null) return JsValue.FromNumber(0);
                    n.InnerText = txt ?? "";
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("setInnerXML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";
                string frag = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    if (n == null) return JsValue.FromNumber(0);
                    n.InnerXml = frag ?? "";
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("appendXML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";
                string fragText = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    if (n == null) return JsValue.FromNumber(0);

                    var frag = st.Doc.CreateDocumentFragment();
                    frag.InnerXml = fragText ?? "";
                    n.AppendChild(frag);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("prependXML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";
                string fragText = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    if (n == null) return JsValue.FromNumber(0);

                    var frag = st.Doc.CreateDocumentFragment();
                    frag.InnerXml = fragText ?? "";

                    // Insert all children of fragment before first child
                    var first = n.FirstChild;
                    while (frag.FirstChild != null)
                    {
                        var child = frag.FirstChild;
                        frag.RemoveChild(child);
                        n.InsertBefore(child, first);
                    }

                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("getAttr", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.Null();
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns) as XmlElement;
                    if (n == null) return JsValue.Null();
                    if (!n.HasAttribute(name)) return JsValue.Null();
                    return JsValue.FromString(n.GetAttribute(name) ?? "");
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            xml.AddMethod("setAttr", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";
                string value = (a.Length > 2) ? (a[2].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns) as XmlElement;
                    if (n == null) return JsValue.FromNumber(0);
                    n.SetAttribute(name, value ?? "");
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("removeAttr", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns) as XmlElement;
                    if (n == null) return JsValue.FromNumber(0);
                    if (!n.HasAttribute(name)) return JsValue.FromNumber(0);
                    n.RemoveAttribute(name);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.AddMethod("remove", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string xp = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_xml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("XML state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    var n = st.Doc.SelectSingleNode(xp, st.Ns);
                    if (n == null) return JsValue.FromNumber(0);
                    if (n.ParentNode == null) return JsValue.FromNumber(0);
                    n.ParentNode.RemoveChild(n);
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            xml.DeclareToGlobals();

        }

        using (JsClass json = _js.CreateClass("JSON"))
        {
            json.AddMethod("constructor", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                int id = _jsonNextId++;
                var st = new JsonState();
                _json[id] = st;

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("_id", JsValue.FromNumber(id));
                obj.Set("error", JsValue.Null());

                if (a.Length > 0 && a[0].Type == Kind.String)
                {
                    try
                    {
                        st.Root = JsonNode.Parse(a[0].String ?? "") ?? new JsonObject();
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                    }
                }

                return JsValue.Null();
            });

            json.AddMethod("parse", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("JSON state missing."));
                    return JsValue.FromNumber(0);
                }

                string text = (a.Length > 0) ? (a[0].String ?? "") : "";
                try
                {
                    st.Root = JsonNode.Parse(text) ?? new JsonObject();
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            json.AddMethod("json", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st)) return JsValue.Null();

                return JsValue.FromString(st.Root?.ToJsonString(new JsonSerializerOptions { WriteIndented = false }) ?? "null");
            });

            json.AddMethod("pretty", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st)) return JsValue.Null();

                return JsValue.FromString(st.Root?.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) ?? "null");
            });

            json.AddMethod("get", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("JSON state missing."));
                    return JsValue.Null();
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.Null();
                }

                var node = GetNode(st.Root, segs);
                return JsonToJs(node);
            });

            json.AddMethod("getJson", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("JSON state missing."));
                    return JsValue.Null();
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.Null();
                }

                var node = GetNode(st.Root, segs);
                if (node == null) return JsValue.Null();
                return JsValue.FromString(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            });

            json.AddMethod("set", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                string path = (a.Length > 0) ? (a[0].String ?? "") : "";
                JsValue val = (a.Length > 1) ? a[1] : JsValue.Null();
                bool parseJson = (a.Length > 2 && a[2].Type == Kind.Number && (int)a[2].Number != 0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("JSON state missing."));
                    return JsValue.FromNumber(0);
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.FromNumber(0);
                }

                var node = JsToJsonNode(val, parseJson, out var verr);
                if (verr.Length > 0)
                {
                    obj.Set("error", JsValue.FromString(verr));
                    return JsValue.FromNumber(0);
                }

                if (!GetParent(st, segs, create: true, out var parent, out var last, out var rerr))
                {
                    obj.Set("error", JsValue.FromString(rerr));
                    return JsValue.FromNumber(0);
                }
                if (parent == null)
                {
                    obj.Set("error", JsValue.FromString("Parent missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    if (last is string key)
                    {
                        if (parent is not JsonObject jo) { obj.Set("error", JsValue.FromString("Target is not an object.")); return JsValue.FromNumber(0); }
                        jo[key] = node;
                    }
                    else
                    {
                        int idx = (int)last;
                        if (parent is not JsonArray ja) { obj.Set("error", JsValue.FromString("Target is not an array.")); return JsValue.FromNumber(0); }
                        while (ja.Count <= idx) ja.Add(null);
                        ja[idx] = node;
                    }
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            json.AddMethod("setJson", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero)
                    return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("JSON state missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    // setJson(text)  -> whole document
                    if (a.Length == 1)
                    {
                        string text = (a[0].Type == Kind.String) ? (a[0].String ?? "") : (a[0].String ?? "");
                        st.Root = JsonNode.Parse(text) ?? new JsonObject();
                        return JsValue.FromNumber(1);
                    }

                    // setJson(path, fragmentJson) -> set at path, fragment is parsed as JSON
                    if (a.Length >= 2)
                    {
                        string path = (a[0].Type == Kind.String) ? (a[0].String ?? "") : "";
                        string frag = (a[1].Type == Kind.String) ? (a[1].String ?? "") : (a[1].String ?? "");

                        if (!TryParsePath(path, out var segs, out var perr))
                        {
                            obj.Set("error", JsValue.FromString(perr));
                            return JsValue.FromNumber(0);
                        }

                        JsonNode? node;
                        try
                        {
                            node = JsonNode.Parse(frag);
                        }
                        catch (Exception ex2)
                        {
                            obj.Set("error", JsValue.FromString(ex2.Message));
                            return JsValue.FromNumber(0);
                        }

                        if (!GetParent(st, segs, create: true, out var parent, out var last, out var rerr))
                        {
                            obj.Set("error", JsValue.FromString(rerr));
                            return JsValue.FromNumber(0);
                        }
                        if (parent == null)
                        {
                            obj.Set("error", JsValue.FromString("Parent missing."));
                            return JsValue.FromNumber(0);
                        }

                        if (last is string key)
                        {
                            if (parent is not JsonObject jo)
                            {
                                obj.Set("error", JsValue.FromString("Target is not an object."));
                                return JsValue.FromNumber(0);
                            }
                            jo[key] = node;
                        }
                        else
                        {
                            int idx = (int)last;
                            if (parent is not JsonArray ja)
                            {
                                obj.Set("error", JsValue.FromString("Target is not an array."));
                                return JsValue.FromNumber(0);
                            }
                            while (ja.Count <= idx) ja.Add(null);
                            ja[idx] = node;
                        }

                        return JsValue.FromNumber(1);
                    }

                    obj.Set("error", JsValue.FromString("setJson expects 1 or 2 arguments."));
                    return JsValue.FromNumber(0);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            json.AddMethod("exists", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st)) return JsValue.FromNumber(0);

                if (!TryParsePath(path, out var segs, out _)) return JsValue.FromNumber(0);
                return JsValue.FromNumber(GetNode(st.Root, segs) != null ? 1 : 0);
            });

            json.AddMethod("type", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st)) return JsValue.Null();

                if (!TryParsePath(path, out var segs, out _)) return JsValue.Null();
                var n = GetNode(st.Root, segs);
                if (n == null) return JsValue.FromString("null");
                if (n is JsonArray) return JsValue.FromString("array");
                if (n is JsonObject) return JsValue.FromString("object");
                if (n is JsonValue jv)
                {
                    if (jv.TryGetValue<string>(out _)) return JsValue.FromString("string");
                    if (jv.TryGetValue<bool>(out _)) return JsValue.FromString("bool");
                    return JsValue.FromString("number");
                }
                return JsValue.FromString("unknown");
            });

            json.AddMethod("length", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st)) return JsValue.FromNumber(0);

                if (!TryParsePath(path, out var segs, out _)) return JsValue.FromNumber(0);
                var n = GetNode(st.Root, segs);

                if (n is JsonArray ja) return JsValue.FromNumber(ja.Count);
                if (n is JsonObject jo) return JsValue.FromNumber(jo.Count);
                if (n is JsonValue jv && jv.TryGetValue<string>(out var s)) return JsValue.FromNumber((s ?? "").Length);
                return JsValue.FromNumber(0);
            });

            json.AddMethod("keys", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st)) return JsValue.Null();

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.Null();
                }

                var n = GetNode(st.Root, segs);
                if (n is not JsonObject jo) return JsValue.Null();

                var arr = new JsonArray();
                foreach (var kv in jo) arr.Add(kv.Key);
                return JsValue.FromString(arr.ToJsonString());
            });

            json.AddMethod("remove", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("JSON state missing."));
                    return JsValue.FromNumber(0);
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.FromNumber(0);
                }

                if (segs.Count == 0) return JsValue.FromNumber(0);

                if (!GetParent(st, segs, create: false, out var parent, out var last, out var rerr))
                {
                    obj.Set("error", JsValue.FromString(rerr));
                    return JsValue.FromNumber(0);
                }
                if (parent == null) return JsValue.FromNumber(0);

                try
                {
                    if (last is string key)
                    {
                        if (parent is not JsonObject jo) return JsValue.FromNumber(0);
                        return JsValue.FromNumber(jo.Remove(key) ? 1 : 0);
                    }
                    else
                    {
                        int idx = (int)last;
                        if (parent is not JsonArray ja) return JsValue.FromNumber(0);
                        if (idx < 0 || idx >= ja.Count) return JsValue.FromNumber(0);
                        ja.RemoveAt(idx);
                        return JsValue.FromNumber(1);
                    }
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            json.AddMethod("push", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                string path = (a.Length > 0) ? (a[0].String ?? "") : "";
                JsValue val = (a.Length > 1) ? a[1] : JsValue.Null();
                bool parseJson = (a.Length > 2 && a[2].Type == Kind.Number && (int)a[2].Number != 0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_json.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("JSON state missing."));
                    return JsValue.FromNumber(0);
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.FromNumber(0);
                }

                var node = JsToJsonNode(val, parseJson, out var verr);
                if (verr.Length > 0)
                {
                    obj.Set("error", JsValue.FromString(verr));
                    return JsValue.FromNumber(0);
                }

                // ensure target exists as array
                var target = GetNode(st.Root, segs);
                if (target == null)
                {
                    // create container at path
                    if (!GetParent(st, segs, create: true, out var parent, out var last, out var rerr))
                    {
                        obj.Set("error", JsValue.FromString(rerr));
                        return JsValue.FromNumber(0);
                    }
                    if (parent == null)
                    {
                        obj.Set("error", JsValue.FromString("Parent missing."));
                        return JsValue.FromNumber(0);
                    }

                    var newArr = new JsonArray();
                    if (last is string key)
                    {
                        if (parent is not JsonObject jo) { obj.Set("error", JsValue.FromString("Target is not an object.")); return JsValue.FromNumber(0); }
                        jo[key] = newArr;
                    }
                    else
                    {
                        int idx = (int)last;
                        if (parent is not JsonArray ja) { obj.Set("error", JsValue.FromString("Target is not an array.")); return JsValue.FromNumber(0); }
                        while (ja.Count <= idx) ja.Add(null);
                        ja[idx] = newArr;
                    }

                    target = newArr;
                }

                if (target is not JsonArray arr)
                {
                    obj.Set("error", JsValue.FromString("Target is not an array."));
                    return JsValue.FromNumber(0);
                }

                arr.Add(node);
                return JsValue.FromNumber(arr.Count);
            });

            json.AddMethod("free", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                return JsValue.FromNumber((id >= 0 && _json.Remove(id)) ? 1 : 0);
            });

            json.DeclareToGlobals();
        }

        using (JsClass http = _js.CreateClass("HTTPRequest"))
        {
            // Encoding Resolver (wie bei File, aber wiederverwendbar hier)
            Func<string?, Encoding> ResolveEncoding = (encRaw) =>
            {
                string e = (encRaw ?? "utf-8").Trim().ToLowerInvariant();
                return e switch
                {
                    "utf-8" or "utf8" or "utf-8-nobom" or "utf8-nobom" => new UTF8Encoding(false),
                    "utf-8-bom" or "utf8-bom" => new UTF8Encoding(true),

                    "utf-16" or "utf16" or "utf-16le" or "utf16le" or "unicode" => new UnicodeEncoding(false, false),
                    "utf-16-bom" or "utf16-bom" or "utf-16le-bom" or "utf16le-bom" or "unicode-bom" => new UnicodeEncoding(false, true),

                    "utf-16be" or "utf16be" => new UnicodeEncoding(true, false),
                    "utf-16be-bom" or "utf16be-bom" => new UnicodeEncoding(true, true),

                    "utf-32" or "utf32" or "utf-32le" or "utf32le" => new UTF32Encoding(false, false),
                    "utf-32-bom" or "utf32-bom" or "utf-32le-bom" or "utf32le-bom" => new UTF32Encoding(false, true),

                    "utf-32be" or "utf32be" => new UTF32Encoding(true, false),
                    "utf-32be-bom" or "utf32be-bom" => new UTF32Encoding(true, true),

                    _ => Encoding.GetEncoding(encRaw ?? "utf-8")
                };
            };

            // Request-Headers aus:
            //  - string (Header-Zeilen)
            //  - oder object (wenn JsObject Keys-API vorhanden -> per Reflection probieren)
            Action<JsObject, HttpRequestMessage, HttpContent?> ApplyHeaders = (obj, req, content) =>
            {
                JsValue hv = obj.Get("Headers");

                // 1) Headers als String: "A: b\nC: d"
                if (hv.Type == Kind.String)
                {
                    string raw = hv.String ?? "";
                    var lines = raw.Replace("\r", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        int p = line.IndexOf(':');
                        if (p <= 0) continue;
                        string name = line.Substring(0, p).Trim();
                        string val = line.Substring(p + 1).Trim();
                        if (name.Length == 0) continue;

                        if (!req.Headers.TryAddWithoutValidation(name, val))
                            content?.Headers.TryAddWithoutValidation(name, val);
                    }
                    return;
                }

                // 2) Headers als JS Object: { "User-Agent":"...", "Accept":"..." }
                if (hv.Type == Kind.Object && hv.Handle != IntPtr.Zero)
                {
                    _js.RetainHandle(hv.Handle);
                    using var hobj = new JsObject(_js, hv.Handle, true);

                    // versuch Keys() / GetKeys() / PropertyNames() etc.
                    IEnumerable<string> keys = Array.Empty<string>();

                    var t = hobj.GetType();
                    var m =
                        t.GetMethod("Keys", BindingFlags.Public | BindingFlags.Instance)
                        ?? t.GetMethod("GetKeys", BindingFlags.Public | BindingFlags.Instance)
                        ?? t.GetMethod("GetPropertyNames", BindingFlags.Public | BindingFlags.Instance)
                        ?? t.GetMethod("PropertyNames", BindingFlags.Public | BindingFlags.Instance);

                    if (m != null)
                    {
                        object? r = m.Invoke(hobj, null);
                        if (r is string[] sa) keys = sa;
                        else if (r is IEnumerable<string> es) keys = es;
                        else if (r is IEnumerable<object> eo) keys = eo.Select(x => x?.ToString() ?? "");
                    }

                    foreach (var k in keys)
                    {
                        if (string.IsNullOrWhiteSpace(k)) continue;
                        JsValue vv = hobj.Get(k);
                        string val = vv.String ?? "";
                        if (!req.Headers.TryAddWithoutValidation(k, val))
                            content?.Headers.TryAddWithoutValidation(k, val);
                    }
                }
            };

            Func<HttpResponseMessage, string> BuildHeaderString = (resp) =>
            {
                var sb = new StringBuilder();

                foreach (var h in resp.Headers)
                    sb.Append(h.Key).Append(": ").Append(string.Join(", ", h.Value)).Append('\n');

                foreach (var h in resp.Content.Headers)
                    sb.Append(h.Key).Append(": ").Append(string.Join(", ", h.Value)).Append('\n');

                return sb.ToString();
            };

            Func<string, bool> IsTextContentType = (ct) =>
            {
                string x = (ct ?? "").ToLowerInvariant();
                return x.StartsWith("text/")
                    || x.Contains("json")
                    || x.Contains("xml")
                    || x.Contains("html")
                    || x.Contains("javascript");
            };

            http.AddMethod("constructor", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                int id = _httpNextId++;
                _http[id] = new HttpState();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                obj.Set("_id", JsValue.FromNumber(id));
                obj.Set("error", JsValue.Null());

                obj.Set("Method", JsValue.FromString("GET"));
                obj.Set("Url", JsValue.FromString(""));
                obj.Set("ContentType", JsValue.FromString(""));

                // kann string oder object sein
                obj.Set("Headers", JsValue.FromString(""));

                // Body ist string (Text) oder Base64 wenn Binary=1
                obj.Set("Body", JsValue.FromString(""));

                obj.Set("Encoding", JsValue.FromString("utf-8"));
                obj.Set("Binary", JsValue.FromNumber(0));       // 1 => Body ist Base64 / Response Base64
                obj.Set("TimeoutMs", JsValue.FromNumber(30000)); // default 30s

                // Upload-“Streaming”: openstream() macht Buffer, writestream() füllt, closestream() sendet
                obj.Set("StreamUpload", JsValue.FromNumber(0)); // 1 => Upload-Buffer aktiv

                // Response Infos
                obj.Set("Status", JsValue.FromNumber(0));
                obj.Set("StatusText", JsValue.FromString(""));
                obj.Set("ResponseContentType", JsValue.FromString(""));
                obj.Set("ResponseHeaders", JsValue.FromString(""));
                obj.Set("ResponseBase64", JsValue.FromString(""));
                obj.Set("ResponseText", JsValue.FromString(""));

                return JsValue.Null();
            });

            // Send() => string (Text oder Base64, je nach Binary/ContentType)
            http.AddMethod("send", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_http.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTTP state missing."));
                    return JsValue.Null();
                }

                string url = obj.Get("Url").String ?? "";
                if (string.IsNullOrWhiteSpace(url))
                {
                    obj.Set("error", JsValue.FromString("Url is empty."));
                    return JsValue.Null();
                }

                string m = (obj.Get("Method").String ?? "GET").Trim();
                if (m.Length == 0) m = "GET";

                string ct = obj.Get("ContentType").String ?? "";
                string body = obj.Get("Body").String ?? "";

                int timeoutMs = (obj.Get("TimeoutMs").Type == Kind.Number) ? (int)obj.Get("TimeoutMs").Number : 30000;
                if (timeoutMs <= 0) timeoutMs = 30000;

                bool reqBinary = (obj.Get("Binary").Type == Kind.Number) && ((int)obj.Get("Binary").Number != 0);
                Encoding enc = ResolveEncoding(obj.Get("Encoding").String);

                try
                {
                    using var cts = new CancellationTokenSource(timeoutMs);

                    var req = new HttpRequestMessage(new HttpMethod(m.ToUpperInvariant()), url);

                    HttpContent? content = null;

                    // Body nur wenn da ist ODER Method typisch Body hat
                    bool wantsBody = body.Length > 0 && !string.Equals(m, "GET", StringComparison.OrdinalIgnoreCase)
                                               && !string.Equals(m, "HEAD", StringComparison.OrdinalIgnoreCase);

                    if (wantsBody)
                    {
                        byte[] data = reqBinary ? Convert.FromBase64String(body) : enc.GetBytes(body);
                        content = new ByteArrayContent(data);

                        string useCt = ct;
                        if (string.IsNullOrWhiteSpace(useCt))
                            useCt = reqBinary ? "application/octet-stream" : "text/plain; charset=" + enc.WebName;

                        content.Headers.TryAddWithoutValidation("Content-Type", useCt);
                        req.Content = content;
                    }

                    ApplyHeaders(obj, req, content);

                    using var resp = _httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token)
                                               .GetAwaiter().GetResult();

                    obj.Set("Status", JsValue.FromNumber((int)resp.StatusCode));
                    obj.Set("StatusText", JsValue.FromString(resp.ReasonPhrase ?? ""));
                    obj.Set("ResponseHeaders", JsValue.FromString(BuildHeaderString(resp)));

                    string respCt = resp.Content.Headers.ContentType?.ToString() ?? "";
                    obj.Set("ResponseContentType", JsValue.FromString(respCt));

                    byte[] rb = resp.Content.ReadAsByteArrayAsync(cts.Token).GetAwaiter().GetResult();

                    bool isText = !reqBinary && IsTextContentType(respCt);

                    if (!isText)
                    {
                        string b64 = Convert.ToBase64String(rb);
                        obj.Set("ResponseBase64", JsValue.FromString(b64));
                        obj.Set("ResponseText", JsValue.FromString(""));
                        return JsValue.FromString(b64);
                    }
                    else
                    {
                        // charset aus response wenn vorhanden, sonst Request-Encoding
                        Encoding respEnc = enc;
                        string? cs = resp.Content.Headers.ContentType?.CharSet;
                        if (!string.IsNullOrWhiteSpace(cs))
                        {
                            try { respEnc = Encoding.GetEncoding(cs); } catch { /* fallback */ }
                        }

                        string txt = respEnc.GetString(rb);
                        obj.Set("ResponseText", JsValue.FromString(txt));
                        obj.Set("ResponseBase64", JsValue.FromString(""));
                        return JsValue.FromString(txt);
                    }
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // openstream()
            // - wenn StreamUpload=1: öffnet Upload-Buffer (writestream -> buffer)
            // - sonst: sendet request und öffnet Response-Stream zum readstream()
            http.AddMethod("openstream", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_http.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTTP state missing."));
                    return JsValue.FromNumber(0);
                }

                // schon offen?
                if (st.RespStream != null || st.Upload != null)
                {
                    obj.Set("error", JsValue.FromString("Stream already open."));
                    return JsValue.FromNumber(0);
                }

                bool streamUpload = (obj.Get("StreamUpload").Type == Kind.Number) && ((int)obj.Get("StreamUpload").Number != 0);
                bool binary = (obj.Get("Binary").Type == Kind.Number) && ((int)obj.Get("Binary").Number != 0);
                Encoding enc = ResolveEncoding(obj.Get("Encoding").String);

                if (streamUpload)
                {
                    st.Upload = new MemoryStream();
                    st.UploadEnc = enc;
                    st.UploadBinary = binary;
                    return JsValue.FromNumber(1);
                }

                string url = obj.Get("Url").String ?? "";
                if (string.IsNullOrWhiteSpace(url))
                {
                    obj.Set("error", JsValue.FromString("Url is empty."));
                    return JsValue.FromNumber(0);
                }

                string m = (obj.Get("Method").String ?? "GET").Trim();
                if (m.Length == 0) m = "GET";

                string ct = obj.Get("ContentType").String ?? "";
                string body = obj.Get("Body").String ?? "";

                int timeoutMs = (obj.Get("TimeoutMs").Type == Kind.Number) ? (int)obj.Get("TimeoutMs").Number : 30000;
                if (timeoutMs <= 0) timeoutMs = 30000;

                try
                {
                    using var cts = new CancellationTokenSource(timeoutMs);

                    var req = new HttpRequestMessage(new HttpMethod(m.ToUpperInvariant()), url);

                    HttpContent? content = null;
                    bool wantsBody = body.Length > 0 && !string.Equals(m, "GET", StringComparison.OrdinalIgnoreCase)
                                               && !string.Equals(m, "HEAD", StringComparison.OrdinalIgnoreCase);

                    if (wantsBody)
                    {
                        byte[] data = binary ? Convert.FromBase64String(body) : enc.GetBytes(body);
                        content = new ByteArrayContent(data);

                        string useCt = ct;
                        if (string.IsNullOrWhiteSpace(useCt))
                            useCt = binary ? "application/octet-stream" : "text/plain; charset=" + enc.WebName;

                        content.Headers.TryAddWithoutValidation("Content-Type", useCt);
                        req.Content = content;
                    }

                    ApplyHeaders(obj, req, content);

                    // ResponseHeadersRead => streambar
                    var resp = _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cts.Token)
                                         .GetAwaiter().GetResult();

                    st.Resp = resp;
                    //st.RespStream = resp.Content.ReadAsStream(cts.Token).GetAwaiter().GetResult();
                    st.RespStream = resp.Content.ReadAsStreamAsync(cts.Token).GetAwaiter().GetResult();


                    obj.Set("Status", JsValue.FromNumber((int)resp.StatusCode));
                    obj.Set("StatusText", JsValue.FromString(resp.ReasonPhrase ?? ""));
                    obj.Set("ResponseHeaders", JsValue.FromString(BuildHeaderString(resp)));

                    string respCt = resp.Content.Headers.ContentType?.ToString() ?? "";
                    obj.Set("ResponseContentType", JsValue.FromString(respCt));

                    bool isText = !binary && IsTextContentType(respCt);
                    st.StreamBinary = !isText;

                    if (!st.StreamBinary)
                    {
                        Encoding respEnc = enc;
                        string? cs = resp.Content.Headers.ContentType?.CharSet;
                        if (!string.IsNullOrWhiteSpace(cs))
                        {
                            try { respEnc = Encoding.GetEncoding(cs); } catch { }
                        }

                        st.RespReader = new StreamReader(st.RespStream, respEnc, detectEncodingFromByteOrderMarks: true, bufferSize: 4096, leaveOpen: true);
                    }

                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // readstream([n]) -> chunk (string) oder Base64 (wenn binary) oder null bei EOF
            http.AddMethod("readstream", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_http.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTTP state missing."));
                    return JsValue.Null();
                }

                int n = 4096;
                if (a.Length > 0 && a[0].Type == Kind.Number)
                {
                    n = (int)a[0].Number;
                    if (n <= 0) n = 4096;
                    if (n > 1024 * 1024) n = 1024 * 1024;
                }

                try
                {
                    if (st.RespStream == null)
                    {
                        obj.Set("error", JsValue.FromString("No open response stream."));
                        return JsValue.Null();
                    }

                    if (st.StreamBinary)
                    {
                        byte[] buf = new byte[n];
                        int r = st.RespStream.Read(buf, 0, buf.Length);
                        if (r <= 0) return JsValue.Null();
                        if (r != buf.Length) Array.Resize(ref buf, r);
                        return JsValue.FromString(Convert.ToBase64String(buf));
                    }
                    else
                    {
                        if (st.RespReader == null)
                        {
                            obj.Set("error", JsValue.FromString("Text reader not initialized."));
                            return JsValue.Null();
                        }

                        char[] cb = new char[n];
                        int r = st.RespReader.Read(cb, 0, cb.Length);
                        if (r <= 0) return JsValue.Null();
                        return JsValue.FromString(new string(cb, 0, r));
                    }
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.Null();
                }
            });

            // writestream(data) -> 1/0  (nur wenn StreamUpload=1 und openstream() gemacht)
            // - wenn Binary=1: data ist Base64
            // - sonst: data ist Text (Encoding property)
            http.AddMethod("writestream", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_http.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTTP state missing."));
                    return JsValue.FromNumber(0);
                }

                if (st.Upload == null)
                {
                    obj.Set("error", JsValue.FromString("No open upload stream. Set StreamUpload=1 and call openstream() first."));
                    return JsValue.FromNumber(0);
                }

                if (a.Length < 1)
                {
                    obj.Set("error", JsValue.FromString("Missing data."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    string s = a[0].String ?? "";

                    if (st.UploadBinary)
                    {
                        byte[] data = Convert.FromBase64String(s);
                        st.Upload.Write(data, 0, data.Length);
                    }
                    else
                    {
                        byte[] data = st.UploadEnc.GetBytes(s);
                        st.Upload.Write(data, 0, data.Length);
                    }

                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // closestream()
            // - wenn Upload offen: sendet request mit Buffer und gibt Response (Text/Base64) zurück
            // - wenn Response-Stream offen: schließt und gibt 1 zurück
            http.AddMethod("closestream", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_http.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("HTTP state missing."));
                    return JsValue.Null();
                }

                // 1) Upload-Mode => jetzt senden + response komplett lesen
                if (st.Upload != null)
                {
                    string url = obj.Get("Url").String ?? "";
                    if (string.IsNullOrWhiteSpace(url))
                    {
                        obj.Set("error", JsValue.FromString("Url is empty."));
                        st.Upload.Dispose();
                        st.Upload = null;
                        return JsValue.Null();
                    }

                    string m = (obj.Get("Method").String ?? "POST").Trim();
                    if (m.Length == 0) m = "POST";

                    string ct = obj.Get("ContentType").String ?? "";
                    int timeoutMs = (obj.Get("TimeoutMs").Type == Kind.Number) ? (int)obj.Get("TimeoutMs").Number : 30000;
                    if (timeoutMs <= 0) timeoutMs = 30000;

                    bool reqBinary = (obj.Get("Binary").Type == Kind.Number) && ((int)obj.Get("Binary").Number != 0);
                    Encoding enc = ResolveEncoding(obj.Get("Encoding").String);

                    try
                    {
                        using var cts = new CancellationTokenSource(timeoutMs);

                        byte[] payload = st.Upload.ToArray();
                        st.Upload.Dispose();
                        st.Upload = null;

                        var req = new HttpRequestMessage(new HttpMethod(m.ToUpperInvariant()), url);
                        var content = new ByteArrayContent(payload);

                        string useCt = ct;
                        if (string.IsNullOrWhiteSpace(useCt))
                            useCt = reqBinary ? "application/octet-stream" : "text/plain; charset=" + enc.WebName;

                        content.Headers.TryAddWithoutValidation("Content-Type", useCt);
                        req.Content = content;

                        ApplyHeaders(obj, req, content);

                        using var resp = _httpClient.SendAsync(req, HttpCompletionOption.ResponseContentRead, cts.Token)
                                                   .GetAwaiter().GetResult();

                        obj.Set("Status", JsValue.FromNumber((int)resp.StatusCode));
                        obj.Set("StatusText", JsValue.FromString(resp.ReasonPhrase ?? ""));
                        obj.Set("ResponseHeaders", JsValue.FromString(BuildHeaderString(resp)));

                        string respCt = resp.Content.Headers.ContentType?.ToString() ?? "";
                        obj.Set("ResponseContentType", JsValue.FromString(respCt));

                        byte[] rb = resp.Content.ReadAsByteArrayAsync(cts.Token).GetAwaiter().GetResult();

                        bool isText = !reqBinary && IsTextContentType(respCt);

                        if (!isText)
                        {
                            string b64 = Convert.ToBase64String(rb);
                            obj.Set("ResponseBase64", JsValue.FromString(b64));
                            obj.Set("ResponseText", JsValue.FromString(""));
                            return JsValue.FromString(b64);
                        }
                        else
                        {
                            Encoding respEnc = enc;
                            string? cs = resp.Content.Headers.ContentType?.CharSet;
                            if (!string.IsNullOrWhiteSpace(cs))
                            {
                                try { respEnc = Encoding.GetEncoding(cs); } catch { }
                            }

                            string txt = respEnc.GetString(rb);
                            obj.Set("ResponseText", JsValue.FromString(txt));
                            obj.Set("ResponseBase64", JsValue.FromString(""));
                            return JsValue.FromString(txt);
                        }
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.Null();
                    }
                }

                // 2) Response-Stream Mode => schließen
                try
                {
                    st.RespReader?.Dispose();
                    st.RespReader = null;

                    st.RespStream?.Dispose();
                    st.RespStream = null;

                    st.Resp?.Dispose();
                    st.Resp = null;

                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            // optional: free()
            http.AddMethod("free", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0) return JsValue.FromNumber(0);

                if (_http.TryGetValue(id, out var st))
                {
                    try
                    {
                        st.RespReader?.Dispose();
                        st.RespStream?.Dispose();
                        st.Resp?.Dispose();
                        st.Upload?.Dispose();
                    }
                    catch { }
                }

                return JsValue.FromNumber(_http.Remove(id) ? 1 : 0);
            });

            http.DeclareToGlobals();

        }

        using (JsClass yaml = _js.CreateClass("YAML"))
        {
            yaml.AddMethod("constructor", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                int id = _yamlNextId++;
                var st = new YamlState();
                _yaml[id] = st;

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("_id", JsValue.FromNumber(id));
                obj.Set("error", JsValue.Null());

                if (a.Length > 0 && a[0].Type == Kind.String)
                {
                    try
                    {
                        var o = _yamlDes.Deserialize<object>(a[0].String ?? "");
                        st.Root = YamlObjToJsonNode(o) ?? new JsonObject();
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                    }
                }

                return JsValue.Null();
            });

            yaml.AddMethod("parse", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_yaml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("YAML state missing."));
                    return JsValue.FromNumber(0);
                }

                string text = (a.Length > 0) ? (a[0].String ?? "") : "";
                try
                {
                    var o = _yamlDes.Deserialize<object>(text);
                    st.Root = YamlObjToJsonNode(o) ?? new JsonObject();
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            yaml.AddMethod("yaml", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_yaml.TryGetValue(id, out var st)) return JsValue.Null();

                string y = _yamlSer.Serialize(JsonNodeToYamlObj(st.Root));
                return JsValue.FromString(y.TrimEnd());
            });

            yaml.AddMethod("get", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_yaml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("YAML state missing."));
                    return JsValue.Null();
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.Null();
                }

                var node = GetNode(st.Root, segs);
                return YamlToJs(node);
            });

            // set(path, value, parseYamlFlag)
            yaml.AddMethod("set", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                string path = (a.Length > 0) ? (a[0].String ?? "") : "";
                JsValue val = (a.Length > 1) ? a[1] : JsValue.Null();
                bool parseYaml = (a.Length > 2 && a[2].Type == Kind.Number && (int)a[2].Number != 0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_yaml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("YAML state missing."));
                    return JsValue.FromNumber(0);
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.FromNumber(0);
                }
                if (segs.Count == 0)
                {
                    obj.Set("error", JsValue.FromString("Empty target path. Use parse() to replace whole document."));
                    return JsValue.FromNumber(0);
                }

                var node = JsToYamlNode(val, parseYaml, out var verr);
                if (verr.Length > 0)
                {
                    obj.Set("error", JsValue.FromString(verr));
                    return JsValue.FromNumber(0);
                }

                if (!GetParent(st, segs, create: true, out var parent, out var last, out var rerr))
                {
                    obj.Set("error", JsValue.FromString(rerr));
                    return JsValue.FromNumber(0);
                }
                if (parent == null)
                {
                    obj.Set("error", JsValue.FromString("Parent missing."));
                    return JsValue.FromNumber(0);
                }

                try
                {
                    if (last is string key)
                    {
                        if (parent is not JsonObject jo) { obj.Set("error", JsValue.FromString("Target is not an object.")); return JsValue.FromNumber(0); }
                        jo[key] = node;
                    }
                    else
                    {
                        int idx = (int)last;
                        if (parent is not JsonArray ja) { obj.Set("error", JsValue.FromString("Target is not an array.")); return JsValue.FromNumber(0); }
                        while (ja.Count <= idx) ja.Add(null);
                        ja[idx] = node;
                    }
                    return JsValue.FromNumber(1);
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            yaml.AddMethod("remove", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string path = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_yaml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("YAML state missing."));
                    return JsValue.FromNumber(0);
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.FromNumber(0);
                }
                if (segs.Count == 0) return JsValue.FromNumber(0);

                if (!GetParent(st, segs, create: false, out var parent, out var last, out _))
                    return JsValue.FromNumber(0);
                if (parent == null) return JsValue.FromNumber(0);

                try
                {
                    if (last is string key)
                    {
                        if (parent is not JsonObject jo) return JsValue.FromNumber(0);
                        return JsValue.FromNumber(jo.Remove(key) ? 1 : 0);
                    }
                    else
                    {
                        int idx = (int)last;
                        if (parent is not JsonArray ja) return JsValue.FromNumber(0);
                        if (idx < 0 || idx >= ja.Count) return JsValue.FromNumber(0);
                        ja.RemoveAt(idx);
                        return JsValue.FromNumber(1);
                    }
                }
                catch (Exception ex)
                {
                    obj.Set("error", JsValue.FromString(ex.Message));
                    return JsValue.FromNumber(0);
                }
            });

            yaml.AddMethod("push", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                string path = (a.Length > 0) ? (a[0].String ?? "") : "";
                JsValue val = (a.Length > 1) ? a[1] : JsValue.Null();
                bool parseYaml = (a.Length > 2 && a[2].Type == Kind.Number && (int)a[2].Number != 0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);
                obj.Set("error", JsValue.Null());

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                if (id < 0 || !_yaml.TryGetValue(id, out var st))
                {
                    obj.Set("error", JsValue.FromString("YAML state missing."));
                    return JsValue.FromNumber(0);
                }

                if (!TryParsePath(path, out var segs, out var perr))
                {
                    obj.Set("error", JsValue.FromString(perr));
                    return JsValue.FromNumber(0);
                }

                var node = JsToYamlNode(val, parseYaml, out var verr);
                if (verr.Length > 0)
                {
                    obj.Set("error", JsValue.FromString(verr));
                    return JsValue.FromNumber(0);
                }

                var target = GetNode(st.Root, segs);
                if (target == null)
                {
                    // create array at path
                    if (!GetParent(st, segs, create: true, out var parent, out var last, out var rerr))
                    {
                        obj.Set("error", JsValue.FromString(rerr));
                        return JsValue.FromNumber(0);
                    }
                    if (parent == null)
                    {
                        obj.Set("error", JsValue.FromString("Parent missing."));
                        return JsValue.FromNumber(0);
                    }

                    var newArr = new JsonArray();
                    if (last is string key)
                    {
                        if (parent is not JsonObject jo) { obj.Set("error", JsValue.FromString("Target is not an object.")); return JsValue.FromNumber(0); }
                        jo[key] = newArr;
                    }
                    else
                    {
                        int idx = (int)last;
                        if (parent is not JsonArray ja) { obj.Set("error", JsValue.FromString("Target is not an array.")); return JsValue.FromNumber(0); }
                        while (ja.Count <= idx) ja.Add(null);
                        ja[idx] = newArr;
                    }
                    target = newArr;
                }

                if (target is not JsonArray arr)
                {
                    obj.Set("error", JsValue.FromString("Target is not an array."));
                    return JsValue.FromNumber(0);
                }

                arr.Add(node);
                return JsValue.FromNumber(arr.Count);
            });

            yaml.AddMethod("free", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);

                _js.RetainHandle(thisVal.Handle);
                using var obj = new JsObject(_js, thisVal.Handle, true);

                int id = (obj.Get("_id").Type == Kind.Number) ? (int)obj.Get("_id").Number : -1;
                return JsValue.FromNumber((id >= 0 && _yaml.Remove(id)) ? 1 : 0);
            });

            yaml.DeclareToGlobals();
        }

        #endregion

        string code = File.ReadAllText(args[0]);

        string last = _js.Run(code);

        return 0;

    }

}
