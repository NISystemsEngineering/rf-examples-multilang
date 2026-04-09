echo on
"C:\git\LabVIEW_Bluetooth_Examples\BLE DTM Test\Nordic Cmd Line Utilities\nrfutil.exe" device recover --serial-number 1050336144
"C:\git\LabVIEW_Bluetooth_Examples\BLE DTM Test\Nordic Cmd Line Utilities\nrfutil.exe" device program --serial-number 1050336144 --firmware "C:\git\LabVIEW_Bluetooth_Examples\BLE DTM Test\hex files\dtm_with_bootloader.hex" --options chip_erase_mode=ERASE_ALL,verify=VERIFY_READ
pause