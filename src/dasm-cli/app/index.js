"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
///<re
const dasm_1 = require("dasm");
const commander = require("commander");
const exists = require("exists-file");
const fs = require("fs");
const path = require("path");
console.log("dasm-cli 2018 (C) Yuuki Wesp");
let prog = commander
    .version("0.0.0", "-v, --version")
    .usage('[options]')
    .option('-i <file>', 'input file to comile')
    .option('-o <file>', 'compiled output file')
    .parse(process.argv);
if (!process.argv.slice(2).length) {
    prog.outputHelp();
    process.exit(-1);
}
let input = commander.I;
let output = commander.O;
if (!output)
    output = "out.bin";
console.log(output);
let name = path.extname(path.basename(output)).trim().replace(".", "");
if (!exists.sync(input)) {
    console.log(`err: file '${input}' not found.`);
    process.exit(-1);
}
let code = fs.readFileSync(input, 'utf8');
let binary = dasm_1.default(code);
binary.output.forEach(element => {
    if (element && element != ' ')
        console.log(`[DASM] =>  ${element}`);
});
if (!binary.success) {
    console.log("failed compile.");
    process.exit(-1);
}
if (binary.listRaw)
    fs.writeFileSync(`${name}.lt`, binary.listRaw);
console.log(`[DASM] list raw data ==> '${name}.lt'`);
if (binary.data)
    fs.writeFileSync(`${output}`, binary.data);
//# sourceMappingURL=index.js.map