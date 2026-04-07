echo on
"C:\git\LabVIEW_Bluetooth_Examples\BLE DTM Test\Nordic Cmd Line Utilities\nrfutil.exe" device program --serial-number 1050336144 --firmware "C:\git\LabVIEW_Bluetooth_Examples\BLE DTM Test\hex files\nRF_Connect_Programmer_1775258769112.hex" --options chip_erase_mode=ERASE_ALL --traits jlink
pause