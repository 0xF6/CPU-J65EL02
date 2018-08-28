import { Terminal } from 'xterm';
import {enableLigatures} from 'xterm-addon-ligatures';

var xterm = new Terminal()
xterm.open(document.getElementById('terminal'));
enableLigatures(xterm);
xterm.writeln("-> {.} >>= etc");