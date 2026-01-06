let io = new Console();

let f1 = new File("in.txt", "r");
let f2 = new File("out.txt", "w+");

let data = f1.read();
f2.write(data);
f2.write("Ich bin Datei B!\n");

io.print("in.txt:\n" + f1.read());
io.print("\nout.txt:\n" + f2.read());
