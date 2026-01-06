let io = new Console(); 

io.print("Hi"); 

let h = """<div id="a"><span class="x">Hi</span></div>"""; 

print(h); 

let d = new HTML(h); 

io.print("text: " + d.innerText("span.x") + "\n"); 
io.print("innerHTML(div#a): " + d.innerHTML("div#a") + "\n"); 

d.setAttr("span.x", "data-test", "123"); 
io.print("attr: " + d.getAttr("span.x", "data-test") + "\n"); 

d.appendHTML("div#a", "<b>!</b>"); 
d.setInnerText("span.x", "Hallo <welt>"); // wird escaped 

io.print("\nRESULT:\n" + d.html() + "\n");
