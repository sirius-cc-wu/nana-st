-work

variable station
variable addr1
variable addr2
variable reg1
variable reg2
variable crc
variable mb-channel
variable mb-position

\ 建立array
: mb-array ( n -- ) ( i -- addr)
    create cells allot
    does> swap cells + ;
100 mb-array data

\ CRC計算的最後進行位元對調
: mb-byteswap ( n -- n')
    dup  $00ff and 8 LSHIFT
    swap $ff00 and 8 RSHIFT
	or
;

\ CRC16的計算
: mb-crc16 ( len -- n')
	$ffff
	swap 0 
	do
		$ffff and
		i data @ xor
		8 0
		do dup 1 and
			if 1 RSHIFT
				$a001 xor
			else 1 RSHIFT then
		loop
	loop
	mb-byteswap
;

\ 將值寫入variable
: mb-write-variable ( n1 n2 n3 n4 n5 --)
	mb-position ! 
	mb-channel !
	station ! 
	dup 256 mod addr2 ! 256 / addr1 ! 
	dup 256 mod reg2 ! 256 / reg1 ! 
;

\ 將variable的值寫入array
: mb-write-data ( --)
	station @ 0 data !
	$06 1 data !
	addr1 @ 2 data !
	addr2 @ 3 data !
	reg1 @ 4 data !
	reg2 @ 5 data !
;

\ 執行功能碼06的指令
: mb-fc06 ( reg addr station channel position -- )
	mb-write-variable
	mb-write-data
	6 mb-crc16 crc !
	station @ $06 addr1 @ addr2 @ reg1 @ reg2 @ crc @ 256 / crc @ 256 mod 8 mb-channel @ mb-position @ uart-data!
;

: set-v! ( F: v -- )
	10000e f* 9.5e f/ f>s $0000 $01 modbus-ch-slv mb-fc06
;

: set-i! ( F: i -- )
	10000e f* 9.5e f/ f>s $0001 $01 modbus-ch-slv mb-fc06
;
\ 01 06 0000 2710 => $2710 $0000 $01 1 1 fc06 (if channel = 1 and position = 1)

\ 目標電壓電流 目前設定的電壓電流
fvariable target-v
fvariable target-i
fvariable cur-v
fvariable cur-i
\ 設定電源的sfc
\ 0.1秒設定一次
: set-pw-v-idle
	( wait )
;

: set-pw-v
	target-v f@ fdup
	cur-v f! set-v!
;

: set-pw-i-idle
	( wait )
;

\ 1000A 則要輸入9.5v 0.475對應50A
\ 設定較高電流時以50A慢慢往上設定
: set-pw-i
	target-i f@ cur-i f@ f- 0.475e f> if
		cur-i f@ 0.475e f+ fdup
		cur-i f! set-i!
	else
		target-i f@ fdup
		cur-i f! set-i!
	then
;


step set-pw-v-idle
step set-pw-v
step set-pw-i-idle
step set-pw-i


: set-pw-v-wait-ok?
	['] set-pw-v-idle elapsed 100 >
;

: set-pw-v-true
	true
;

: set-pw-i-wait-ok?
	['] set-pw-i-idle elapsed 100 >
;
: set-pw-i-true
	true
;
transition set-pw-v-wait-ok?
transition set-pw-v-true
transition set-pw-i-wait-ok?
transition set-pw-i-true

' set-pw-v-idle        ' set-pw-v-wait-ok?          -->
' set-pw-v-wait-ok?    ' set-pw-v				    -->
' set-pw-v	           ' set-pw-v-true    	        -->
' set-pw-v-true        ' set-pw-i-idle				-->

' set-pw-i-idle        ' set-pw-i-wait-ok?          -->
' set-pw-i-wait-ok?    ' set-pw-i				    -->
' set-pw-i	           ' set-pw-i-true    	        -->
' set-pw-i-true        ' set-pw-v-idle				-->

marker -work