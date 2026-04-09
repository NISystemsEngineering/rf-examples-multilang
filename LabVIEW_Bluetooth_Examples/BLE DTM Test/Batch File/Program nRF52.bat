echo on
"<nordicUtilExePath>" device recover --serial-number 1050336144
"<nordicUtilExePath>" device program --serial-number <serialNumber> --firmware "<hexFilePath>" --options chip_erase_mode=ERASE_ALL,verify=VERIFY_READ
pause