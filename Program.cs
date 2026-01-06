using Fizzler.Systems.HtmlAgilityPack;
using HtmlAgilityPack;
using MiniJsHost;
using System;
using System.Globalization;
using System.IO;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;

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

    public static int Main(string[] args)
    {

        if (args.Length < 1)
        {
            Console.WriteLine("Usage: MiniJS_Demo.exe <scriptfile>");
            return 1;
        }

        _js = new MiniJs();

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

                MiniJs js = _js;
                js.RetainHandle(thisVal.Handle);

                string outstr = String.Empty;

                foreach (JsValue arg in args)
                {
                    outstr += arg.String;
                }
                Console.Write(outstr);
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
            // new Html([htmlString])
            html.AddMethod("constructor", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                string s = (a.Length > 0 && a[0].Type == Kind.String) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("_html", JsValue.FromString(s));
                    obj.Set("error", JsValue.Null());
                }
                return JsValue.Null();
            });

            // setHtml(string)
            html.AddMethod("setHtml", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                string s = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    obj.Set("_html", JsValue.FromString(s));
                }
                return JsValue.Null();
            });

            // html() -> whole html
            html.AddMethod("html", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    return JsValue.FromString(obj.Get("_html").String ?? "");
                }
            });

            // outerHTML(selector) -> string|null
            html.AddMethod("outerHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null) return JsValue.Null();

                        return JsValue.FromString(node.OuterHtml ?? "");
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.Null();
                    }
                }
            });

            // innerHTML(selector) -> string|null
            html.AddMethod("innerHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null) return JsValue.Null();

                        return JsValue.FromString(node.InnerHtml ?? "");
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.Null();
                    }
                }
            });

            // innerText(selector) -> string|null
            html.AddMethod("innerText", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null) return JsValue.Null();

                        return JsValue.FromString(node.InnerText ?? "");
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.Null();
                    }
                }
            });

            // getAttr(selector, name) -> string|null
            html.AddMethod("getAttr", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.Null();
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string name = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null) return JsValue.Null();

                        var attr = node.Attributes[name];
                        if (attr == null) return JsValue.Null();

                        return JsValue.FromString(attr.Value ?? "");
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.Null();
                    }
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
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null)
                        {
                            obj.Set("error", JsValue.FromString("Selector not found."));
                            return JsValue.FromNumber(0);
                        }

                        node.SetAttributeValue(name, value);
                        obj.Set("_html", JsValue.FromString(doc.DocumentNode.OuterHtml ?? ""));
                        return JsValue.FromNumber(1);
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.FromNumber(0);
                    }
                }
            });

            // setInnerHTML(selector, htmlFragment) -> 1/0
            html.AddMethod("setInnerHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string frag = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null)
                        {
                            obj.Set("error", JsValue.FromString("Selector not found."));
                            return JsValue.FromNumber(0);
                        }

                        node.InnerHtml = frag;
                        obj.Set("_html", JsValue.FromString(doc.DocumentNode.OuterHtml ?? ""));
                        return JsValue.FromNumber(1);
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.FromNumber(0);
                    }
                }
            });

            // setInnerText(selector, text) -> 1/0 (wird escaped)
            html.AddMethod("setInnerText", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string txt = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null)
                        {
                            obj.Set("error", JsValue.FromString("Selector not found."));
                            return JsValue.FromNumber(0);
                        }

                        node.InnerHtml = HtmlEntity.Entitize(txt); // sicherer als raw InnerHtml
                        obj.Set("_html", JsValue.FromString(doc.DocumentNode.OuterHtml ?? ""));
                        return JsValue.FromNumber(1);
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.FromNumber(0);
                    }
                }
            });

            // appendHTML(selector, htmlFragment) -> 1/0
            html.AddMethod("appendHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string frag = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null)
                        {
                            obj.Set("error", JsValue.FromString("Selector not found."));
                            return JsValue.FromNumber(0);
                        }

                        node.InnerHtml = (node.InnerHtml ?? "") + frag;
                        obj.Set("_html", JsValue.FromString(doc.DocumentNode.OuterHtml ?? ""));
                        return JsValue.FromNumber(1);
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.FromNumber(0);
                    }
                }
            });

            // prependHTML(selector, htmlFragment) -> 1/0
            html.AddMethod("prependHTML", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";
                string frag = (a.Length > 1) ? (a[1].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null)
                        {
                            obj.Set("error", JsValue.FromString("Selector not found."));
                            return JsValue.FromNumber(0);
                        }

                        node.InnerHtml = frag + (node.InnerHtml ?? "");
                        obj.Set("_html", JsValue.FromString(doc.DocumentNode.OuterHtml ?? ""));
                        return JsValue.FromNumber(1);
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.FromNumber(0);
                    }
                }
            });

            // remove(selector) -> 1/0
            html.AddMethod("remove", (JsValue[] a, JsValue thisVal) =>
            {
                if (thisVal.Type != Kind.Object || thisVal.Handle == IntPtr.Zero) return JsValue.FromNumber(0);
                string sel = (a.Length > 0) ? (a[0].String ?? "") : "";

                _js.RetainHandle(thisVal.Handle);
                using (JsObject obj = new JsObject(_js, thisVal.Handle, true))
                {
                    obj.Set("error", JsValue.Null());
                    string src = obj.Get("_html").String ?? "";

                    try
                    {
                        var doc = new HtmlDocument();
                        doc.LoadHtml(src);

                        var node = doc.DocumentNode.QuerySelector(sel);
                        if (node == null) return JsValue.FromNumber(0);

                        node.Remove();
                        obj.Set("_html", JsValue.FromString(doc.DocumentNode.OuterHtml ?? ""));
                        return JsValue.FromNumber(1);
                    }
                    catch (Exception ex)
                    {
                        obj.Set("error", JsValue.FromString(ex.Message));
                        return JsValue.FromNumber(0);
                    }
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

        string code = File.ReadAllText(args[0]);

        string last = _js.Run(code);

        return 0;

    }

}
