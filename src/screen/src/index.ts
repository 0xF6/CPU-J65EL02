import { app, BrowserWindow, ipcMain, ipcRenderer } from 'electron';
let win: BrowserWindow = null;
var express = require('express');
var appw = express();

appw.listen(1337, function(){
    console.log('Express server listening on port 1337');
});

app.on("ready", () => {
    win = new BrowserWindow({
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

app.on('window-all-closed', () => {
    if (process.platform != 'darwin') {
        app.quit();
    }
});