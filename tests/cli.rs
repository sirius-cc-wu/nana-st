use std::fs;
use std::path::PathBuf;

use nana_st::cli::{CliError, parse_compile_command, run};

fn args(parts: &[&str]) -> Vec<String> {
    parts.iter().map(|part| (*part).to_owned()).collect()
}

#[test]
fn parses_a_compile_command_with_an_output_path() {
    let command = parse_compile_command(&args(&[
        "compile",
        "program.st",
        "--output",
        "program.wasm",
    ]))
    .expect("compile command should parse");

    assert_eq!(command.input, PathBuf::from("program.st"));
    assert_eq!(command.output, PathBuf::from("program.wasm"));
}

#[test]
fn rejects_a_compile_command_without_an_output_path() {
    let error = parse_compile_command(&args(&["compile", "program.st"]))
        .expect_err("missing output should be rejected");

    assert!(matches!(error, CliError::Usage(_)));
}

#[test]
fn reports_an_unreadable_source_file() {
    let error = run(&args(&[
        "compile",
        "does-not-exist.st",
        "--output",
        "program.wasm",
    ]))
    .expect_err("missing input should fail");

    assert!(matches!(error, CliError::ReadSource { .. }));
}

#[test]
fn does_not_write_output_when_compilation_fails() {
    let test_directory = std::env::temp_dir().join(format!("nana-st-cli-{}", std::process::id()));
    let _ = fs::remove_dir_all(&test_directory);
    fs::create_dir_all(&test_directory).expect("test directory should be created");

    let source = test_directory.join("program.st");
    let output = test_directory.join("program.wasm");
    fs::write(&source, "PROGRAM Main END_PROGRAM").expect("source should be written");

    let error = run(&args(&[
        "compile",
        source.to_str().expect("test path should be UTF-8"),
        "--output",
        output.to_str().expect("test path should be UTF-8"),
    ]))
    .expect_err("the compiler facade is not implemented yet");

    assert!(matches!(error, CliError::Compile { .. }));
    assert!(!output.exists());

    fs::remove_dir_all(test_directory).expect("test directory should be removed");
}
