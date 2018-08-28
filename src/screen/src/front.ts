import { Terminal } from 'xterm';

import * as fit from 'xterm/lib/addons/fit/fit';
import * as webLinks from 'xterm/lib/addons/webLinks/webLinks';
import * as winptyCompat from 'xterm/lib/addons/winptyCompat/winptyCompat';

import {enableLigatures} from 'xterm-addon-ligatures';

Terminal.applyAddon(fit);
Terminal.applyAddon(webLinks);
Terminal.applyAddon(winptyCompat);
const {ipcRenderer} = require('electron')
var xterm = new Terminal()
ipcRenderer.on("char", (event, arg) => {
    xterm.write(arg.char);
})
xterm.open(document.getElementById('terminal'));
xterm.resize(80, 65);
(xterm as any).webLinksInit();
(xterm as any).winptyCompatInit();
xterm.setOption("termName", "vts100");
enableLigatures(xterm);
xterm.writeln("Screen J65EL02 - 2018 (C) Yuuki Wesp -->");