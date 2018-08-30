import { app, BrowserWindow, ipcMain } from 'electron';
let win: BrowserWindow = null;
var express = require('express');
import * as bodyParser from "body-parser";
var appw = express();
var jsonParser = bodyParser.json()
appw.post('/char',jsonParser, function (req, res) {
    if (!req.body) return res.sendStatus(405);
    if (!req.body.char) return res.sendStatus(400);
    win.webContents.send("char", {char: req.body.char});
    res.send({status: 0x0});
});
appw.get('/status',jsonParser, function (req, res) {
    res.send({status: 0x0});
});
appw.listen(8666, function(){});

app.on("ready", () => {
    win = new BrowserWindow({
        width: 800, height: 583
    });
    win.loadURL(`file://${__dirname}/../assets/index.html`);
    
    win.setMenu(null);
    win.setResizable(true);
    //win.webContents.openDevTools();
    win.on("closed", () => {
        win = null;
    });
});

app.on('window-all-closed', () => {
    if (process.platform != 'darwin') {
        app.quit();
    }
});