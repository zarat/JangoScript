print("=== Process Poll + Task/Await/Yield Test (WORKING) ===");
setTaskLimit(4);

function log(tag, msg) {
  lock("stdout") { print("[" + tag + "] " + msg); }
}

function runPing(tag, ip) {
  let p = new Process();
  p.FileName = "ping";
  p.Arguments = ip + " -n 3";
  p.MergeErr = 1;

  if (!p.start()) {
    log(tag, "START FAILED: " + p.error);
    return { out: "", code: -999 };
  }

  log(tag, "PID=" + p.Pid + " started");

  let out = "";

  // poll solange running
  while (p.isRunning()) {
    let chunk = p.read();     // non-blocking
    if (chunk != "") {
      out += chunk;
      log(tag, "chunk:\n" + chunk);
    } else {
      yield; // wichtig: sonst 100% CPU busy-spin beim pollen
    }
  }

  // drain rest
  while (true) {
    let tail = p.read();
    if (tail == "") break;
    out += tail;
    log(tag, "tail:\n" + tail);
    yield;
  }

  let code = p.exitCode();
  log(tag, "EXIT=" + code);

  // optional cleanup
  p.free();

  return { out: out, code: code };
}

let t1 = task { return runPing("P1", "1.1.1.1"); };
let t2 = task { return runPing("P2", "8.8.8.8"); };

let tick = 0;
while (t1.status != "Done" || t2.status != "Done") {
  tick++;
  lock("stdout") { print("[MAIN] tick " + tick + "  t1=" + t1.status + "  t2=" + t2.status); }
  yield;
}

let r1 = await t1;
let r2 = await t2;

print("=== SUMMARY ===");
print("P1 exit=" + r1.code + " len=" + r1.out.length);
print("P2 exit=" + r2.code + " len=" + r2.out.length);
print("=== END ===");
