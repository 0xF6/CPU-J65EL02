"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const electron_1 = require("electron");
const amq = require("amqplib");
let win = null;
amq.connect("amqp://localhost:8490").then((con) => {
    con.createChannel().then((ch) => {
        ch.assertQueue("screen", { durable: false });
        ch.consume("screen", function (msg) {
            console.log(" [x] Received %s", msg.content.toString());
        }, { noAck: true });
    });
}, (err) => {
    console.log(err);
});
electron_1.app.on("ready", () => {
    win = new electron_1.BrowserWindow({
        width: 1200, height: 860
    });
    win.loadURL(`file://${__dirname}/../assets/index.html`);
    win.setMenu(null);
    win.setResizable(false);
    win.webContents.openDevTools();
    win.on("closed", () => {
        win = null;
    });
});
electron_1.app.on('window-all-closed', () => {
    if (process.platform != 'darwin') {
        electron_1.app.quit();
    }
});
//# sourceMappingURL=index.js.map