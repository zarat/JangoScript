let io = new Console();

// Beispiel HTML-Dokument laden
let html = """
<html>
  <head><title>Demo</title></head>
  <body>
    <h1 id="title">Beispielseite</h1>

    <div class="content">
      <p>Erster Absatz mit <b>fett</b> und <i>kursiv</i>.</p>
      <p>Zweiter Absatz mit einem <a href="https://example.com">Link</a>.</p>
    </div>

    <ul id="nav">
      <li><a href="/home">Home</a></li>
      <li><a href="/about">Über uns</a></li>
      <li><a href="/contact">Kontakt</a></li>
    </ul>
  </body>
</html>
""";

let d = new HTML(html);

//
// Grundfunktionen
//
io.print("=== Grundfunktionen ===\n");
io.print("Titel: ", d.innerText("title"), "\n");
io.print("H1 Text: ", d.innerText("h1#title"), "\n");
io.print("InnerHTML der .content:\n", d.innerHTML(".content"), "\n\n");

//
// Liste aller <a>-Elemente ausgeben
//
io.print("=== Alle <a>-Elemente ===\n");

let count = d.count("a");
io.print("Gefundene Links: ", count, "\n");

for (let i = 0; i < count; i++) {
  let txt = d.innerTextAt("a", i);
  let href = d.getAttrAt("a", i, "href");
  io.print("a[", i, "]: ", txt, " (", href, ")\n");
}

//
// Attribute setzen, Text ersetzen
//
io.print("\n=== Attribute / Text ändern ===\n");

// Beispiel: Link öffnen in neuem Tab
d.setAttrAt("a", 0, "target", "_blank");
// Beispiel: Text von H1 ändern
d.setInnerText("h1#title", "HTML-Testseite");
// Beispiel: HTML in .content anhängen
d.appendHTML(".content", "<p>Dritter Absatz mit <u>Unterstreichung</u>.</p>");

//
// Elemente löschen
//
io.print("\n=== Elemente löschen ===\n");
d.removeAt("ul#nav li", 1);  // „Über uns“ entfernen
io.print("Neuer Navigationsblock:\n", d.innerHTML("#nav"), "\n\n");

//
// Neues Element einfügen
//
io.print("=== Neues Element einfügen ===\n");
d.prependHTML("body", "<div class='notice'> Dies ist eine Demo-Seite.</div>");

//
// Komplettes Ergebnis ausgeben
//
io.print("\n=== Ergebnis-HTML ===\n");
io.print(d.html(), "\n");

//
// Fehlerbehandlung (Test ungültiger Selector)
//
io.print("\n=== Fehler-Test ===\n");
d.setInnerText("xyz", "fail");
io.print("error: ", d.error, "\n");
