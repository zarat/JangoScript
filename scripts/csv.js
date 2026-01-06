let io = new Console();

let csvText = """Name;Age;Tags
Max;33;"a,b"
Anna;27;"x"
Bob;41;"k,l,m"
""";

let t = new CSV();

// Input-Settings
t.ReadSep = ";";
t.HasHeader = 1;

if (t.parse(csvText) == 0) {
  io.print("CSV parse ERROR: ", t.error, "\n");
} else {
  io.print("Rows=", t.rowCount(), " Cols=", t.colCount(), "\n");
  io.print("Header0=", t.header(0), " Header1=", t.header(1), " Header2=", t.header(2), "\n");

  // Zugriff per Index
  io.print("Row1 Col0=", t.get(1, 0), "  (Name)\n");
  io.print("Row1 Col2=", t.get(1, 2), "  (Tags raw)\n");

  // Zugriff per Header-Name
  io.print("Row0 Age(by name)=", t.getByName(0, "Age"), "\n");

  // Spalte hinzufügen + befüllen
  let colCity = t.addColumn("City");
  t.set(0, colCity, "Wien");
  t.set(1, colCity, "Linz");
  t.set(2, colCity, "Graz");

  // Zeile hinzufügen + setByName
  let r = t.addRow();
  t.setByName(r, "Name", "Zoe");
  t.setByName(r, "Age", "19");
  t.setByName(r, "Tags", "new");
  t.setByName(r, "City", "Salzburg");

  // Output-Settings
  t.WriteSep = ",";
  t.WriteHeader = 1;

  io.print("\n--- CSV OUT (comma) ---\n");
  io.print(t.toCsv(), "\n");
}

// Error-Demo: kaputter CSV (nur um error zu zeigen)
let bad = new CSV();
bad.ReadSep = ";";
bad.HasHeader = 1;

bad.parse("A;B\n\"unterminated\n1;2\n");
io.print("\nBad CSV error=", bad.error, "\n");
