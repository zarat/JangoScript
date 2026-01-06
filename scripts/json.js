let io = new Console();

let j = new JSON();

let src = """
{
  "meta": { "version": "1", "name": "demo" },
  "users": [
    { "id": "1", "name": "Max",  "role": "admin", "tags": ["a","b"] },
    { "id": "2", "name": "Anna", "role": "user",  "tags": [] }
  ]
}
""";

let ok = j.parse(src);
io.print("parse ok=", ok, " err=", j.error, "\n");

io.print("type(/users)=", j.type("/users"), "\n");
io.print("len(/users)=", j.len("/users"), "\n");

io.print("user0.name=", j.get("/users/0/name"), "\n");
io.print("user1.role(dot)=", j.get("users[1].role"), "\n");

io.print("keys(/meta)=", j.keys("/meta"), "\n");

j.set("/meta/version", 2, 0);
j.setJson("/meta/build", "{\"branch\":\"main\",\"commit\":\"abc123\"}");

let newLen = j.push("/users", "{\"id\":3,\"name\":\"Bob\",\"role\":\"guest\",\"tags\":[\"k\"]}", 1);
io.print("users len(after push)=", newLen, "\n");

let rem = j.remove("/users/1");
io.print("remove users[1]=", rem, " len=", j.len("/users"), "\n");

io.print("\n--- JSON OUT (pretty) ---\n");
io.print(j.pretty(), "\n");

j.free();
