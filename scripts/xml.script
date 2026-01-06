let io = new Console();

let x = new XML();
x.setXml("<root><user id=\"1\">Max</user></root>");

io.print("err=" + x.error + "\n");
io.print("exists user=" + x.exists("/root/user") + "\n");
io.print("count user=" + x.count("/root/user") + "\n");
io.print("text=" + x.innerText("/root/user") + "\n");

x.setAttr("/root/user", "id", "2");
io.print("id=" + x.getAttr("/root/user", "id") + "\n");

x.appendXML("/root", "<user id=\"3\">Eva</user>");
io.print("count user=" + x.count("/root/user") + "\n");

io.print("XML=" + x.xml() + "\n");
