use std::fs;
use std::path::PathBuf;
use std::process::Command;

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
fn rejects_an_output_path_that_would_overwrite_the_source() {
    let test_directory =
        std::env::temp_dir().join(format!("nana-st-cli-same-{}", std::process::id()));
    let _ = fs::remove_dir_all(&test_directory);
    fs::create_dir_all(&test_directory).expect("test directory should be created");

    let source = test_directory.join("program.st");
    let source_text = "PROGRAM Main END_PROGRAM";
    fs::write(&source, source_text).expect("source should be written");

    let error = run(&args(&[
        "compile",
        source.to_str().expect("test path should be UTF-8"),
        "--output",
        source.to_str().expect("test path should be UTF-8"),
    ]))
    .expect_err("source and output paths must differ");

    assert!(matches!(error, CliError::Usage(_)));
    assert_eq!(
        fs::read_to_string(&source).expect("source should remain"),
        source_text
    );

    fs::remove_dir_all(test_directory).expect("test directory should be removed");
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
    fs::write(&source, "PROGRAM Main missing := TRUE; END_PROGRAM")
        .expect("source should be written");
    fs::write(&output, b"stale Wasm output").expect("stale output should be written");

    let error = run(&args(&[
        "compile",
        source.to_str().expect("test path should be UTF-8"),
        "--output",
        output.to_str().expect("test path should be UTF-8"),
    ]))
    .expect_err("invalid source should fail compilation");

    assert!(matches!(error, CliError::Compile { .. }));
    assert_eq!(
        fs::read(&output).expect("existing output should be preserved"),
        b"stale Wasm output"
    );

    fs::remove_dir_all(test_directory).expect("test directory should be removed");
}

#[test]
fn rejects_an_output_path_that_aliases_the_source() {
    let test_directory =
        std::env::temp_dir().join(format!("nana-st-cli-alias-{}", std::process::id()));
    let _ = fs::remove_dir_all(&test_directory);
    fs::create_dir_all(test_directory.join("aliases")).expect("test directory should be created");

    let source = test_directory.join("program.st");
    let alias = test_directory.join("aliases/../program.st");
    let source_text = "PROGRAM Main END_PROGRAM";
    fs::write(&source, source_text).expect("source should be written");

    let error = run(&args(&[
        "compile",
        source.to_str().expect("test path should be UTF-8"),
        "--output",
        alias.to_str().expect("test path should be UTF-8"),
    ]))
    .expect_err("output alias should be rejected");

    assert!(matches!(error, CliError::Usage(_)));
    assert_eq!(
        fs::read_to_string(&source).expect("source should remain"),
        source_text
    );

    fs::remove_dir_all(test_directory).expect("test directory should be removed");
}

#[cfg(unix)]
#[test]
fn rejects_a_hard_link_to_the_source_as_output() {
    let test_directory =
        std::env::temp_dir().join(format!("nana-st-cli-hard-link-{}", std::process::id()));
    let _ = fs::remove_dir_all(&test_directory);
    fs::create_dir_all(&test_directory).expect("test directory should be created");

    let source = test_directory.join("program.st");
    let output = test_directory.join("program.wasm");
    let source_text = "PROGRAM Main END_PROGRAM";
    fs::write(&source, source_text).expect("source should be written");
    fs::hard_link(&source, &output).expect("output hard link should be created");

    let error = run(&args(&[
        "compile",
        source.to_str().expect("test path should be UTF-8"),
        "--output",
        output.to_str().expect("test path should be UTF-8"),
    ]))
    .expect_err("source hard link should be rejected");

    assert!(matches!(error, CliError::Usage(_)));
    assert_eq!(
        fs::read_to_string(&source).expect("source should remain"),
        source_text
    );

    fs::remove_dir_all(test_directory).expect("test directory should be removed");
}

#[test]
fn nanastc_binary_writes_wasm_for_valid_source() {
    let test_directory =
        std::env::temp_dir().join(format!("nana-st-cli-success-{}", std::process::id()));
    let _ = fs::remove_dir_all(&test_directory);
    fs::create_dir_all(&test_directory).expect("test directory should be created");

    let source = test_directory.join("program.st");
    let output = test_directory.join("program.wasm");
    fs::write(&source, include_str!("fixtures/pass_through.st")).expect("source should be written");
    fs::write(&output, b"previous output").expect("previous output should be written");

    let result = Command::new(env!("CARGO_BIN_EXE_nanastc"))
        .args([
            "compile",
            source.to_str().expect("test path should be UTF-8"),
            "--output",
            output.to_str().expect("test path should be UTF-8"),
        ])
        .output()
        .expect("nanastc should run");

    assert!(
        result.status.success(),
        "stderr: {}",
        String::from_utf8_lossy(&result.stderr)
    );
    let wasm = fs::read(&output).expect("Wasm output should exist");
    assert_eq!(&wasm[..4], b"\0asm");

    fs::remove_dir_all(test_directory).expect("test directory should be removed");
}
