use nana_st::compile_source;
use wasmtime::{Caller, Engine, Instance, Linker, Module, Store};

#[derive(Debug, PartialEq, Eq)]
enum IoEvent {
    Read(usize),
    Write(usize, i32),
}

#[derive(Default)]
struct FakeBncHost {
    inputs: Vec<i32>,
    outputs: Vec<i32>,
    events: Vec<IoEvent>,
}

fn instantiate(source: &str, host: FakeBncHost) -> (Store<FakeBncHost>, Instance) {
    let wasm = compile_source(source).expect("fixture should compile");
    let engine = Engine::default();
    let module = Module::new(&engine, wasm).expect("module should validate");
    let mut linker = Linker::new(&engine);
    linker
        .func_wrap(
            "bnc",
            "read_input",
            |mut caller: Caller<'_, FakeBncHost>, index: i32| -> i32 {
                let index = usize::try_from(index).expect("input index should be non-negative");
                let host = caller.data_mut();
                host.events.push(IoEvent::Read(index));
                host.inputs.get(index).copied().unwrap_or(0)
            },
        )
        .expect("read input import should link");
    linker
        .func_wrap(
            "bnc",
            "write_output",
            |mut caller: Caller<'_, FakeBncHost>, index: i32, value: i32| {
                let index = usize::try_from(index).expect("output index should be non-negative");
                let host = caller.data_mut();
                host.events.push(IoEvent::Write(index, value));
                if host.outputs.len() <= index {
                    host.outputs.resize(index + 1, 0);
                }
                host.outputs[index] = value;
            },
        )
        .expect("write output import should link");

    let mut store = Store::new(&engine, host);
    let instance = linker
        .instantiate(&mut store, &module)
        .expect("module should instantiate");
    (store, instance)
}

fn call_export(instance: &Instance, store: &mut Store<FakeBncHost>, name: &str) {
    instance
        .get_typed_func::<(), ()>(&mut *store, name)
        .expect("lifecycle export should exist")
        .call(store, ())
        .expect("lifecycle call should succeed");
}

#[test]
fn runs_scan_cycles_with_snapshot_inputs_persistent_state_and_flushed_outputs() {
    let source = r#"
PROGRAM Main
VAR_INPUT
    start : BOOL;
    reset : BOOL;
END_VAR
VAR
    count : INT := 0;
END_VAR
VAR_OUTPUT
    active : BOOL;
    idle : BOOL;
END_VAR

IF reset THEN
    count := 0;
END_IF;
IF start THEN
    count := count + 1;
END_IF;
active := count >= 1;
idle := NOT active;
END_PROGRAM
"#;
    let (mut store, instance) = instantiate(
        source,
        FakeBncHost {
            inputs: vec![0, 0],
            ..FakeBncHost::default()
        },
    );

    call_export(&instance, &mut store, "nana_init");
    call_export(&instance, &mut store, "nana_scan");
    assert_eq!(store.data().outputs, vec![0, 1]);
    assert_eq!(
        store.data().events,
        vec![
            IoEvent::Read(0),
            IoEvent::Read(1),
            IoEvent::Write(0, 0),
            IoEvent::Write(1, 1),
        ]
    );

    store.data_mut().inputs[0] = 1;
    store.data_mut().events.clear();
    call_export(&instance, &mut store, "nana_scan");
    assert_eq!(store.data().outputs, vec![1, 0]);
    assert_eq!(
        store.data().events,
        vec![
            IoEvent::Read(0),
            IoEvent::Read(1),
            IoEvent::Write(0, 1),
            IoEvent::Write(1, 0),
        ]
    );

    store.data_mut().inputs[0] = 0;
    store.data_mut().events.clear();
    call_export(&instance, &mut store, "nana_scan");
    assert_eq!(store.data().outputs, vec![1, 0]);
}

#[test]
fn runs_the_boolean_pass_through_fixture_across_changed_inputs() {
    let (mut store, instance) = instantiate(
        include_str!("fixtures/pass_through.st"),
        FakeBncHost {
            inputs: vec![0],
            ..FakeBncHost::default()
        },
    );

    call_export(&instance, &mut store, "nana_init");
    call_export(&instance, &mut store, "nana_scan");
    assert_eq!(store.data().outputs, vec![0]);

    store.data_mut().inputs[0] = -1;
    call_export(&instance, &mut store, "nana_scan");
    assert_eq!(store.data().outputs, vec![1]);
}

#[test]
fn wraps_runtime_int_arithmetic_before_writing_bnc_output() {
    let source = r#"
PROGRAM Main
VAR
    count : INT := 32767;
END_VAR
VAR_OUTPUT
    output : INT;
END_VAR

count := count + 1;
output := count;
END_PROGRAM
"#;
    let (mut store, instance) = instantiate(source, FakeBncHost::default());

    call_export(&instance, &mut store, "nana_init");
    call_export(&instance, &mut store, "nana_scan");

    assert_eq!(store.data().outputs, vec![-32_768]);
}

#[test]
fn normalizes_bnc_int_inputs_before_program_execution() {
    let source = r#"
PROGRAM Main
VAR_INPUT
    input : INT;
END_VAR
VAR_OUTPUT
    output : INT;
END_VAR

output := input;
END_PROGRAM
"#;
    let (mut store, instance) = instantiate(
        source,
        FakeBncHost {
            inputs: vec![65_535],
            ..FakeBncHost::default()
        },
    );

    call_export(&instance, &mut store, "nana_init");
    call_export(&instance, &mut store, "nana_scan");

    assert_eq!(store.data().outputs, vec![-1]);
}
