import dasm from "dasm";
import * as commander from "commander";
import * as exists from "exists-file";
import * as fs from "fs";
import * as path from "path";
let version = "0.3.0";
console.log(`dasm-cli ${version} 2018 (C) Yuuki Wesp`)
let prog = commander
    .version(version, "-v, --version")
    .usage('[options]')
    .option('-i <file>', 'input file to compile.')
    .option('-o <file>', 'compiled output file.')
    .parse(process.argv);



if (!process.argv.slice(2).length) {
    prog.outputHelp();
    process.exit(-1);
}
let input: string = commander.I;
let output: string = commander.O;

if(!output)
output = "out.bin";
console.log(output);
let name = path.extname(path.basename(output)).trim().replace(".", "");
if(!exists.sync(input))
{
    console.log(`err: file '${input}' not found.`)
    process.exit(-1);
}

let code = fs.readFileSync(input, 'utf8');
let binary = dasm(code);
binary.output.forEach(element => {
    if(element && element != ' ')
    console.log(`[DASM] =>  ${element}`);
});

if(!binary.success)
{
    console.log("failed compile.");
    process.exit(-1);
}
if(binary.listRaw)
    fs.writeFileSync(`${name}.lt`, binary.listRaw);
console.log(`[DASM] list raw data ==> '${name}.lt'`);
if(binary.data)
    fs.writeFileSync(`${output}`, binary.data);
console.log(`[DASM] binary data ==> '${output}'`);