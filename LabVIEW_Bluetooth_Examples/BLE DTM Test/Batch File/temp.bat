echo on
"C:\git\Examples\BLE\LV DTM Receiver Test\Nordic Cmd Line Utilities\nrfutil.exe" device program --serial-number 1050336144 --firmware "C:\git\Examples\BLE\LV DTM Receiver Test\hex files\direct_test_mode_pca10040.hex" --options chip_erase_mode=ERASE_RANGES_TOUCHED_BY_FIRMWARE --traits jlink
pause