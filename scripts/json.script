let io = new Console();

let j = new JSON();

j.parse("""{"users":[{"name":"Max"},{"name":"Eva"}],"flag":true}""");

io.print("name0=" + j.get("users[0].name") + "\n");     // "Max"
io.print("flag=" + j.get("/flag") + "\n");             // 1 (true)

j.set("users[1].name", "Evelyn");
j.push("/users", """{"name":"Tom"}""", 1);              // parseJson=1

io.print(j.pretty() + "\n");
