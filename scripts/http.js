function test1() {
	let req = new HTTPRequest();
	req.Method = "GET";
	req.Url = "https://orf.at";
	req.ContentType = "";
	req.Headers = ""; // oder object, wenn dein JsObject Keys hergibt
	req.Body = "";
	req.Encoding = "utf-8";

	let body = req.Send();
	if (req.error != null) {
	  print("ERR: " + req.error + "\n");
	} else {
	  print("Status=" + req.Status + "\n");
	  print(body + "\n");
	}
}

function test2() {
	let req = new HTTPRequest();
	req.Method = "GET";
	req.Url = "https://orf.at";
	req.Binary = 0; // 1 => Base64 chunks

	if (!req.openstream()) {
	  print("ERR: " + req.error + "\n");
	} else {
	  while (1) {
		let chunk = req.readstream(4096);
		if (chunk == null) break;
		print(chunk);
	  }
	  req.closestream();
	}
}

function test3() {
	let req = new HTTPRequest();
	req.Method = "POST";
	req.Url = "https://httpbin.org/post";
	req.ContentType = "text/plain; charset=utf-8";
	req.Encoding = "utf-8";
	req.StreamUpload = 1;

	if (!req.openstream()) {
	  print("ERR: " + req.error + "\n");
	} else {
	  req.writestream("hello ");
	  req.writestream("world\n");
	  let resp = req.closestream(); // sendet jetzt und gibt response zurück
	  print("Status=" + req.Status + "\n");
	  print(resp + "\n");
	}
}

test1();
test2();
test3();
