import nirfsg
import time
import msvcrt

# --- Configuration ---
resource_name = "RFSA"
file_path = r"C:\Charlie\80211AX_80M.tdms"
center_freq = 5.18e9
power_level = -10.0
external_attenuation = 0.0
waveform_name = "waveform"
# The script must reference the waveform name assigned during download
script_text = f"""
    script GenerateWfm
        repeat forever
            generate {waveform_name}
        end repeat
    end script"""

def run_rfsg_generation():
    # 1. Open NI-RFSG session
    # Note: Use id_query=True, reset_device=False as in your C# code
    with nirfsg.Session(resource_name=resource_name, id_query=True, reset_device=False) as session:
        try:
            # 2. Configure Reference Clock, Frequency, Power, and Gain
            session.configure_rf(frequency=center_freq, power_level=power_level)
            session.frequency_reference_source = nirfsg.FrequencyReferenceSource.ONBOARD_CLOCK
            session.external_gain = -external_attenuation
            session.power_level_type = nirfsg.PowerLevelType.PEAK_POWER
            
            # 3. Read & Download Waveform
            # Python API method name for TDMS playback
            session.read_and_download_waveform_from_file_tdms(
                waveform_name=waveform_name, 
                file_path=file_path
            )
            
            # 4 & 5. Set generation mode to Script and write the script
            session.generation_mode = nirfsg.GenerationMode.SCRIPT
            session.write_script(script_text)
            
            # 6. Initiate signal generation
            session.initiate()
            print("Generation started. Press any key to stop.")

            # 7. Check generation status loop
            while True:
                if msvcrt.kbhit(): 
                    msvcrt.getch() # Clear the key buffer
                    break
                
                # check_generation_status returns True if generation is done
                if session.check_generation_status():
                    break
                
                time.sleep(0.1)

        except nirfsg.Error as e:
            print(f"NI-RFSG Error: {e}")
            
        finally:
            # 8. Abort signal generation
            session.abort()
            
            # 9 & 10. Disable output and Commit
            session.output_enabled = False
            session.commit()
            
            # 11. Clear waveform from memory
            session.clear_arb_waveform(waveform_name)
            print("Session closed.")

if __name__ == "__main__":
    run_rfsg_generation()
