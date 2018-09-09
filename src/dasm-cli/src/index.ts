import dasm from "dasm";
import * as commander from "commander";
import exists from "exists-file";
let prog = commander
    .version("0.0.0", "-v, --version")
    .option('-i, --input-file', 'Input file')
    .option('-o, --output-file', 'Input file')
    .parse(process.argv);

let input = prog.inputFile;
let output = prog.outputFile;

if(!exists.sync(input))
{
    console.log(`err: file '${input}' not found.`)
    process.exit(-1);
}
