echo on
"<nordicUtilExePath>" device program --serial-number <serialNumber> --firmware "<hexFilePath>" --options chip_erase_mode=ERASE_RANGES_TOUCHED_BY_FIRMWARE --traits jlink
pause