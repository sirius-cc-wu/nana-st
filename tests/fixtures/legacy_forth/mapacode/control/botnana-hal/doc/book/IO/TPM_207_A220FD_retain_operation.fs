-work

\ 定義變數
variable tpm-a220fd-slave
variable tpm-a220fd-status

\ Save parameter
: tpm-a220fd-save-parameter ( n -- )
    tpm-a220fd-slave ! 
    $25A5 1 $803A tpm-a220fd-slave @ sdo-download-u16
    begin   
        2 $803A tpm-a220fd-slave @ sdo-upload-u16
        until-no-requests
        tpm-a220fd-slave @ sdo-data@  tpm-a220fd-status !
        tpm-a220fd-status @ $8000 and 0<>
    while
    repeat
    tpm-A220fd-status @ .
    ." log|TPM 207-A220FD Save Parameters"
    tpm-A220fd-status @ $80 and 0= if
        ." (OK)"
    else
        ." (Failed)"
    then
    cr
;


\ Load default parameter
: tpm-a220fd-load-parameter ( n -- )
    tpm-a220fd-slave ! 
    $1A5A 1 $803A tpm-a220fd-slave @ sdo-download-u16
    begin   
        2 $803A tpm-a220fd-slave @ sdo-upload-u16
        until-no-requests
        tpm-a220fd-slave @ sdo-data@  tpm-a220fd-status !
        tpm-a220fd-status @ $8000 and 0<>
    while
    repeat
    tpm-A220fd-status @ .
    ." log|TPM 207-A220FD Load Default Parameters"
    tpm-A220fd-status @ $80 and 0= if
        ." (OK)"
    else
        ." (Failed)"
    then
    cr
;

marker -work
