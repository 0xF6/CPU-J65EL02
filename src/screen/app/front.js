"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
const xterm_1 = require("xterm");
const xterm_addon_ligatures_1 = require("xterm-addon-ligatures");
var xterm = new xterm_1.Terminal();
xterm.open(document.getElementById('terminal'));
xterm_addon_ligatures_1.enableLigatures(xterm);
xterm.writeln("-> {.} >>= etc");
//# sourceMappingURL=front.js.map