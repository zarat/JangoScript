let io = new Console();

function default() {
	
	let xmlText = """
	<root xmlns:h="urn:hello" xmlns:x="urn:x">
	  <h:users>
		<h:user id="1"><h:name>Max</h:name><h:role>admin</h:role></h:user>
		<h:user id="2"><h:name>Anna</h:name><h:role>user</h:role></h:user>
	  </h:users>
	  <x:meta version="1" />
	</root>
	""";

	let x = new XML();
	x.setXml(xmlText);

	// === Namespaces binden ===
	x.addNamespace("h", "urn:hello");
	x.addNamespace("x", "urn:x");

	// === Lesen ===
	io.print("User count=", x.count("//h:user"), "\n");
	io.print("User2 name=", x.innerText("//h:user[@id='2']/h:name"), "\n");
	io.print("meta.version=", x.getAttr("//x:meta", "version"), "\n");

	// === Ändern ===
	x.setAttr("//x:meta", "version", "2");
	x.setInnerText("//h:user[@id='1']/h:name", "Maximilian");

	// === Neues Element anhängen (mit Namespace) ===
	x.appendXML("//h:users",
	"  <h:user id='3'><h:name>Bob</h:name><h:role>guest</h:role></h:user>\n"
	);

	io.print("User count(after append)=", x.count("//h:user"), "\n");

	// === Node löschen ===
	x.remove("//h:user[@id='2']");
	io.print("User count(after remove)=", x.count("//h:user"), "\n");

	// === Komplettes Ergebnis ===
	io.print("\n--- XML OUT ---\n");
	io.print(x.xml(), "\n");

	// === Fehler-Demo ===
	x.innerText("///[");
	io.print("\nXML error=", x.error, "\n");

	x.free();

}

function withNamespace() {
	
	let xmlText = """
	<root>
	  <users>
		<user id="1"><name>Max</name><role>admin</role></user>
		<user id="2"><name>Anna</name><role>user</role></user>
	  </users>
	  <meta version="1" />
	</root>
	""";

	let x = new XML();
	x.setXml(xmlText);

	// === Basics ===
	io.print("User count=", x.count("//user"), "\n");
	io.print("Has meta=", x.exists("//meta"), "\n");
	io.print("User2 name=", x.innerText("//user[@id='2']/name"), "\n");

	// === Attribute lesen/setzen ===
	io.print("meta.version=", x.getAttr("//meta", "version"), "\n");
	x.setAttr("//meta", "version", "2");
	io.print("meta.version(after)=", x.getAttr("//meta", "version"), "\n");

	// === Text ändern ===
	x.setInnerText("//user[@id='1']/name", "Maximilian");
	io.print("User1 name(after)=", x.innerText("//user[@id='1']/name"), "\n");

	// === XML anhängen ===
	x.appendXML("//users",
	"  <user id='3'><name>Bob</name><role>guest</role></user>\n"
	);
	io.print("User count(after append)=", x.count("//user"), "\n");

	// === Node löschen ===
	x.remove("//user[@id='2']");
	io.print("User count(after remove)=", x.count("//user"), "\n");

	// === Komplettes Ergebnis ===
	io.print("\n--- XML OUT ---\n");
	io.print(x.xml(), "\n");

	// === Fehler-Test ===
	x.innerText("///[");
	io.print("\nXML error=", x.error, "\n");

	x.free();

}


default();
withNamespace();
