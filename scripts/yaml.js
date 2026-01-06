let io = new Console();

function hr(title) {
  io.print("\n==================== ", title, " ====================\n");
}

function dump(y, title) {
  io.print("\n--- ", title, " ---\n");
  io.print(y.yaml(), "\n");
}

hr("1) parse() + yaml()");

let y = new YAML();
let ok = y.parse("""
app:
  name: JangoScript
  version: 1
  enabled: true
  tags: [cli, scripting, yaml]
users:
  - name: Alice
    roles: [admin, dev]
  - name: Bob
    roles: [user]
settings:
  retries: 3
  timeoutMs: 1500
  endpoints:
    api: "https://example.local/api"
    auth: "https://example.local/auth"
""");

io.print("parse ok=", ok, " error=", y.error, "\n");
dump(y, "Initial YAML");

hr("2) get() JSON Pointer + dot-path");

io.print("users[0].name = ", y.get("/users/0/name"), "\n");
io.print("users[1].roles[0] = ", y.get("users[1].roles[0]"), "\n");
io.print("settings.endpoints.api = ", y.get("settings.endpoints.api"), "\n");
io.print("app.tags[1] = ", y.get("app.tags[1]"), "\n");

hr("3) set() primitives + parseYaml=1 + auto-create paths");

// einfache Werte
y.set("app.version", 2, 0);
y.set("settings.retries", 5, 0);
y.set("app.tags[2]", "yaml++", 0);

// YAML-String als Wert parsen
y.set("/settings.cache", "enabled: true\nmaxSize: 256", 1);

// neuer verschachtelter Pfad
y.set("/settings/features/0", "fast-start", 0);
y.set("/settings/features/1", "safe-mode", 0);

dump(y, "Nach set()");

hr("4) push() - Elemente anhängen");

// in bestehendes Array
y.push("/users", "name: Charlie\nroles: [tester, guest]", 1);

// neues Array erzeugen
y.push("/logs", "startup", 0);
y.push("/logs", "init done", 0);

dump(y, "Nach push()");

hr("5) remove() - Knoten löschen");

y.remove("/app/tags/1");      // entfernt "scripting"
y.remove("users[0]/roles[0]"); // entfernt "admin"
y.remove("/settings/debug");   // Key löschen

dump(y, "Nach remove()");

hr("6) get() - Kontrolle finaler Werte");

io.print("users[0].name=", y.get("users[0].name"), "\n");
io.print("users[0].roles[0]=", y.get("users[0].roles[0]"), "\n");
io.print("settings.cache.enabled=", y.get("/settings/cache/enabled"), "\n");
io.print("logs[1]=", y.get("/logs/1"), "\n");

hr("7) Fehlerbehandlung");

let bad = new YAML();
bad.parse("a: 1\nb:\n  - :invalid");   // absichtlich falsches YAML
io.print("error=", bad.error, "\n");

hr("Fertig!");
