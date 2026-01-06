let io = new Console();

let f = new File("owa.csv", "r");
f.Encoding = "utf-8";
let text = f.read();

let c = new CSV();
c.ReadSep = ";";
c.WriteSep = ";";
c.HasHeader = 1;
c.WriteHeader = 1;

if (!c.parse(text)) {
  io.print("CSV error: " + c.error + "\n");
} else {
  io.print("rows=" + c.rowCount() + " cols=" + c.colCount() + "\n");
  io.print("col(PrimarySmtpAddress)=" + c.colIndex("PrimarySmtpAddress") + "\n");
  io.print("SamAccountName[0]=" + c.getByName(0, "SamAccountName") + "\n");

  c.setByName(0, "OWAEnabled", "False");
  // serialize:
  let out = c.toCsv();
  let fo = new File("out.csv", "w");
  fo.Encoding = "utf-8";
  fo.write(out);
}
