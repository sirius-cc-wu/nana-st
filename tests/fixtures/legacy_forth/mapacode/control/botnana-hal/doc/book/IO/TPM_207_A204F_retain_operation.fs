-work

\ 定義變數
variable tpm-slave
variable tpm-status

\ Enter TPM retain operation mode
\ n is slave position 
: enter-tpm-operation ( n -- )
    tpm-slave ! 
    0 tpm-status !
    begin
        tpm-status @ $8000 and 0=   
    while       
        $E135 1 $4005 tpm-slave @ sdo-download-u16
        2 $4005 tpm-slave @ sdo-upload-u16
        until-no-requests
        tpm-slave @ sdo-data@ tpm-status ! 
    repeat
    ." log|Enter TPM Retain Operational Mode" cr    
;

\ Leave TPM retain operation mode
: leave-tpm-operation ( n -- )
    tpm-slave ! 
    $8000 tpm-status !
    0 1 $4005 tpm-slave @ sdo-download-u16  
    begin
        tpm-status @ $8000 and 0<>  
    while       
        $E246 1 $4005 tpm-slave @ sdo-download-u16
        2 $4005 tpm-slave @ sdo-upload-u16
        until-no-requests
        tpm-slave @ sdo-data@ tpm-status ! 
    repeat
    ." log|Leave TPM Retain Operational Mode" cr    
;

\ Save parameter to flash memory
: (tpm-save-parameter) ( n -- )
    tpm-slave ! 
    0 1 $4005 tpm-slave @ sdo-download-u16
    $A100 1 $4005 tpm-slave @ sdo-download-u16
    
    begin   
        2 $4005 tpm-slave @ sdo-upload-u16
        until-no-requests
        tpm-slave @ sdo-data@ $4000 and 0<>
    while
    repeat  
    0 1 $4005 tpm-slave @ sdo-download-u16
    ." log|TPM Save parameter" cr  
;

\ Load default parameter
: (tpm-load-parameter) ( n -- )
    tpm-slave ! 
    0 1 $4005 tpm-slave @ sdo-download-u16
    $A200 1 $4005 tpm-slave @ sdo-download-u16
    
    begin   
        2 $4005 tpm-slave @ sdo-upload-u16
        until-no-requests
        tpm-slave @ sdo-data@ $4000 and 0<>
    while
    repeat
    0 1 $4005 tpm-slave @ sdo-download-u16
    ." log|TPM Load Default parameter" cr
;

\ TPM Save parameter
: tpm-save ( n -- )
    dup enter-tpm-operation
    dup (tpm-save-parameter) 
    dup leave-tpm-operation
    drop
;

\ TPM Load parameter
: tpm-load ( n -- )
    dup enter-tpm-operation
    dup (tpm-load-parameter)
    dup leave-tpm-operation
    drop
;

-work marker -work
