# Testing

OpenAK820 has automated tests, plus a manual test plan for what only a real keyboard can show.

## Automated tests

```bash
dotnet test
```

These run without a keyboard and cover:

- **Byte layouts:** golden tests for every frame type. Comments give the address in the vendor binary where each format was read.
- **Command sequences:** the order of frames for each operation, run against a simulated device.
- **App logic:** GIF round trips, audio analysis, effects, saving and loading, conflict detection, and diagnostics privacy.
- **UI smoke tests:** every page is built and painted in every language, and every key function is assigned through the real page code.

Passing them shows the bytes match what was decoded from the vendor program. It doesn't show that a keyboard accepts them, which is what the manual plan is for.

## Try the app without a keyboard

```bash
OpenAK820.exe --simulate
```

This uses a fake keyboard and a temporary settings folder, so it can't change your keyboard or your saved settings.

## Hardware testing so far

Testing has been on one AK820 Pro (`0C45:800A`, firmware family with control on usage page `FF13`). The maintainer reports that virtually everything works, but not every case below has been run, so treat them as unconfirmed unless listed here.

| Area | Confirmed how |
|---|---|
| Finding the keyboard and its HID collections | Read from the device |
| Lighting: mode, colour order, brightness | Keys read back with `F5` |
| Per-key colours | Keys read back with `F5` |
| Live frames: all 81 keys, 16 fps with no drops | Keys read back with `F5` |
| Screen upload: 8 and 132 frames | Every page acknowledged, and the picture checked by eye |

Least tested: 2.4 GHz, and exact macro play-mode values. See [Open questions](PROTOCOL.md#open-questions).

## Manual test plan

168 cases, ordered from safest to riskiest. Stages 7 to 9 overwrite things that can't be read back from the keyboard, so note your own setup first. **Tell us about a failure with [Copy diagnostic info](USER-GUIDE.md#reporting-a-problem).**

Before starting, close the SignalRGB app, OpenRGB and the AJAZZ software, and keep a mouse connected.

Also check that the screen clears its stored animation within a few seconds when the keyboard restarts on USB with the app open.

## Stage 1: App basics

**Almost nothing here touches the keyboard.** The exceptions are A-11 and A-13, which change the stored lighting mode. A-12 and A-13 briefly use SignalRGB.

| ID | Case | Steps | Expected |
|---|---|---|---|
| A-01 | Launch | Open the app with the keyboard connected by USB. | Window opens with 9 pages on the left. Home shows **AK820 Pro (USB)** and **Connected by USB**. |
| A-02 | Visit every page | Click through all 9 pages. | Each page opens straight away, with no error and nothing cut off or overlapping. |
| A-03 | Resize *(Report what you see)* | Drag the window to its smallest size, then maximize it. Check each page at both sizes. | The keyboard drawing scales, and all buttons stay reachable. Note any page where controls are cut off. |
| A-04 | Display scaling *(Report what you see)* | Optional. Set Windows display scale to 125% or 150%, then reopen the app. | Text is sharp and nothing overlaps or gets cut off. |
| A-05 | Close to tray | Click the window's X, then double-click the tray icon. | The window hides, the app keeps running, and the window comes back. |
| A-06 | Exit from tray | Right-click the tray icon, then Exit Program. | The app closes and no OpenAK820 process remains in Task Manager. |
| A-07 | Single instance | With the app open, launch it again. | No second window opens. |
| A-08 | Idle cost *(Report what you see)* | Leave the app idle for a minute with no effect running, and check Task Manager. | CPU stays at or near 0%. Note the memory figure. |
| A-09 | Unplug | Unplug the USB cable and the 2.4 GHz dongle. | Within a second or two, Home shows **No keyboard found**. No error box or crash. |
| A-10 | Replug | Plug the cable back in. | The keyboard is found again without restarting the app. |
| A-11 | Change with no keyboard | With everything unplugged, open Lighting Mode and change the mode. Then replug and set it back. | Bottom bar shows **Saved. No keyboard is connected, so nothing was sent.** |
| A-12 | Conflict banner | Open the SignalRGB app and wait 10 seconds. Close it and wait 10 seconds. | A yellow banner names SignalRGB, then disappears. |
| A-13 | Write during conflict *(Report what you see)* | With SignalRGB streaming, change a lighting mode, then close SignalRGB. | Bottom bar shows a red message about another program, or the write succeeds in a gap. It must not claim success if the lighting didn't change. No crash. |
| A-14 | Profile management | On Home: New, Rename, select between them, then Delete. Try deleting the last one. | Each action updates the list. Deleting the last profile is refused with **At least one profile is needed.** |
| A-15 | Profiles are separate | Make two profiles. In one, on Keys, press Apply on a remap without writing. Switch profiles. | The blue key appears only in the profile you edited. Remove the remap afterwards. |
| A-16 | Settings survive restart | Exit the app fully and reopen it. | Profiles, active profile and language are as you left them. |
| A-17 | Export and import | Export a profile to a file, then Import that file. | A new copy appears in the list with the same settings. |
| A-18 | Bad import | Import a text file renamed to .json. | Red message **That file is not a valid profile.** Nothing added. |
| A-19 | Import safety | Give a key an Open Program action (Apply is enough), export the profile, then import it. Cancel once, then accept. Remove it afterwards. | A warning shows the program path. Cancel adds nothing, and accepting adds the profile. |
| A-20 | Languages *(Report what you see)* | System Settings: switch through all 4 languages, and visit a few pages in each. Finish on your preferred one. | Pages update without a restart. Note any text that's cut off or any wrong translation. |
| A-21 | Autostart | Tick Automatic Start, sign out and back in. Then untick it. | The app starts hidden in the tray. After unticking, it no longer starts. |
| A-22 | Exit on close | Untick Minimize to Tray, click X, and reopen. Then tick it again. | X exits the app when unticked. |
| A-23 | Corrupt settings file | Exit the app. In `%APPDATA%\OpenAK820`, copy `state.json` somewhere safe, then replace its contents with random text. Open the app. Afterwards exit and put your copy back. | The app opens with defaults instead of crashing, and leaves a `state.json.bad` file. |
| A-24 | Simulator mode | Run `OpenAK820.exe --simulate`, and make a change. | Shows AK820 Pro (USB) even if unplugged. The keyboard doesn't react, and your real settings are unchanged. |

## Stage 2: Lighting modes

**Overwrites the stored lighting mode.** You can set it again any time.

| ID | Case | Steps | Expected |
|---|---|---|---|
| L-01 | Static | Pick Static. Try several colours. | Solid colour. Colour and brightness work. Speed and direction are greyed out. |
| L-02 | Animated colour modes *(Report what you see)* | Try SingleOn, SingleOff, Glittering, Falling, Breath, Outward, Explode, Launch, Ripples, Pulsating and Shuttle. | Each one looks different and matches its name. Colour, Colorful, brightness and speed are on, and direction is greyed out. Note any mode that seems wrong. |
| L-03 | Fixed-colour modes | Try Colourful and Spectrum. | Colour and Colorful are greyed out. Brightness and speed work. |
| L-04 | Vertical direction *(Report what you see)* | Pick Scrolling. Switch between Up and Down. | Direction changes. **Does the label match the direction?** The labels are my best guess. |
| L-05 | Horizontal direction *(Report what you see)* | Pick Rolling, Rotating, Flowing and Tilt. Switch between Right and Left on each. | Direction changes. **Do the labels match?** Note any mode that doesn't. |
| L-06 | LED Off | Pick LED Off, then another mode. | Keys go dark and all controls grey out. Lighting returns when you pick another mode. |
| L-07 | Colour picker | On Static: click preset chips, then the large swatch to open the picker. Pick a custom colour. | Keys match the colour. Picking a colour unticks Colorful. Red, green, blue and white all look right. |
| L-08 | Colorful | On Rolling: tick and untick Colorful. | Ticked gives a rainbow, unticked uses the chosen colour. |
| L-09 | Brightness *(Report what you see)* | Step from 5 down to 0 on Static. | Each step dims the keys. **What does level 0 do?** |
| L-10 | Speed *(Report what you see)* | Step from 0 to 5 on Rolling. | Speed changes at each step. **Is 5 the fastest or the slowest?** |
| L-11 | Sending delay | Drag a slider back and forth, then stop. | Keys change about half a second after you stop. The bottom bar shows **Lighting sent to keyboard.** |
| L-12 | Rapid changes | Click through modes quickly for 20 seconds, ending on one you like. | The keyboard ends on the last mode picked, with no red errors. |
| L-13 | Stored on the keyboard | Exit the app, then unplug and replug the keyboard. | The chosen lighting comes back without the app. |

## Stage 3: User lighting (per-key colours)

**Overwrites stored per-key colours** and switches the keyboard to show them. Pick a mode on Lighting Mode to go back.

| ID | Case | Steps | Expected |
|---|---|---|---|
| U-01 | Selecting keys | Click keys, click again, drag from an empty area across some keys, then use Select All and Select None. | Clicks toggle a blue outline, dragging adds a group, and the buttons work. |
| U-02 | Painting | Select keys, choose a colour, Paint selected keys. Try it again with nothing selected. | The drawing updates. With nothing selected, the red message **Select some keys first.** appears. |
| U-03 | Clearing | Select painted keys, then Clear selected keys. | Those keys go back to the plain colour. |
| U-04 | Write to keyboard | Paint a few keys, then Write to keyboard. | Bottom bar confirms. The physical keys match the drawing, and unpainted keys are dark. |
| U-05 | Every key is in the right place *(Report what you see)* | Paint each row a different colour, including Fn, Ctrl, arrows and the Home, PgUp, PgDn column. Write. | **All 81 keys** light up in the right colour. Note any key that's wrong or dark. This is the main key-position check. |
| U-06 | Brightness | Change brightness and Write again. | Keys dim to match. |
| U-07 | Schemes | New, Rename, switch between two schemes, Delete one. | Each scheme keeps its own colours. |
| U-08 | Export and import | Export a scheme, then Import it. Try a bad file too. | The copy matches. A bad file gives a red message and adds nothing. |
| U-09 | Stored on the keyboard | Exit the app, then unplug and replug. | Per-key colours come back without the app. |
| U-10 | Leaving per-key mode | Open the app and pick a different mode on Lighting Mode. If it's already selected, pick another one first. | The keyboard leaves per-key colours. |

## Stage 4: Real-time lighting

**Temporary frames, then your stored lighting is sent again when an effect stops.** Needs USB and the app running.

| ID | Case | Steps | Expected |
|---|---|---|---|
| R-01 | Default rainbow | Press Start. | On-screen keys and the real keys animate together, smoothly. Button changes to Stop. |
| R-02 | Every effect *(Report what you see)* | For each of Static On, Starlight, Fluttering, Dynamic Breathing, Colorful Fountain, Rainbow Wave, Following Currents and Peak Revolving: Select All, choose it, Apply to selected keys, Start, Stop. | Each one looks different and suits its name. Note any you'd want changed. |
| R-03 | Different effects per key | Give half the keys Starlight in red and half Static On in blue. Start. | Each group shows its own effect and colour. |
| R-04 | Turn Off Lighting effect | Apply Turn Off Lighting to some keys. Start. | Those keys are dark while the others run. |
| R-05 | Speed and brightness | While running, move both sliders. | Effect changes live. |
| R-06 | Stop | Press Stop. | Your stored lighting returns. |
| R-07 | Moving around | While running, visit other pages and come back. | Effect keeps running, and the button still says Stop. |
| R-08 | Hidden in tray | While running, close to tray, then reopen. | Effect keeps running. |
| R-09 | Exit during effect | While running, Exit Program from the tray. | Stored lighting returns. |
| R-10 | Typing during effect | Run an effect and type normally for a few minutes. | No missed keys, repeated keys or lag. **This matters most on this page.** |
| R-11 | Long run *(Report what you see)* | Run for at least 10 minutes. Check CPU in Task Manager. | No slowdown, no red errors. Note the CPU figure. |
| R-12 | Unplug during effect *(Report what you see)* | While running, unplug and replug the keyboard. | No crash, and the effect stops. **What do the keys show after replugging: stored lighting or the frozen frame?** |
| R-13 | Killed during effect | While running, end OpenAK820 in Task Manager. Reopen it and change the lighting mode. | Keys freeze when the app is killed, as expected, and recover once you change the mode. |
| R-14 | Schemes | New, Delete, Export, Import. Delete a scheme while it's running. | Each scheme keeps its own settings. Deleting a running scheme stops it cleanly. |

## Stage 5: Music rhythm

**Temporary frames, then your stored lighting is sent again when it stops.** Listens to this PC's audio output and records nothing.

| ID | Case | Steps | Expected |
|---|---|---|---|
| M-01 | Reacts to music | Play music, then press Start. | On-screen keys and real keys react to the audio. |
| M-02 | Silence | Pause the music, with Background Mode set to Off. | Keys fade to dark. |
| M-03 | Rhythms *(Report what you see)* | Try Rising Bars, Centre Bars, Whole-Board Pulse, Rolling Wave, Sparkle and Ripple. | Each one looks different and follows the music. Note any that look wrong. |
| M-04 | Colours | While running, go through the Color list. | Each palette looks right. |
| M-05 | Amplitude | While running, move Amplitude from low to high. | Low gives less movement, high gives more. |
| M-06 | Background | Choose a Background Mode and change its brightness. | Unlit keys show the background colour at that brightness. |
| M-07 | Ambilight | Choose Ambilight as Color or Background, and move a brightly coloured window across your screen. | Keys follow the screen's overall colour, with a short lag. |
| M-08 | Volume and mute *(Report what you see)* | While playing, lower the Windows volume, then mute. | **Do the keys react to volume changes?** Either is fine, tell me which. |
| M-09 | Stop | Press Stop. | Your stored lighting returns. |
| M-10 | Switching engines | Start Real-time, go to Music and press Start. Go back to Real-time. | Music takes over, and Real-time's button says Start. |
| M-11 | Effects list | New, Rename, Delete. Restart the app. | Saved effects keep their settings. |
| M-12 | Typing and CPU *(Report what you see)* | Run with music for a few minutes while typing. | No missed keys or lag. Note the CPU figure. |

## Stage 6: TFT screen

**Replaces the animation on the keyboard's screen when you upload.** The old one can't be read back. Editing alone changes nothing on the keyboard.

| ID | Case | Steps | Expected |
|---|---|---|---|
| S-01 | Import an image | Import a non-square PNG or JPG. | One frame that fills the square, cropped at the sides, with no dark edge. |
| S-02 | Import a GIF | Import an animated GIF. | All frames listed with their delays and a count like **12 of 140 frames**. |
| S-03 | Frame buttons | Create a new frame, Copy frame, Delete frame. Try each with no frames too. | List updates. With no frames, a short message appears and nothing breaks. |
| S-04 | Brush | Draw with fine, medium and thick, in different colours. | Lines follow the mouse in the right size and colour. |
| S-05 | Eraser | Draw over an imported picture, then use Eraser on the drawing. | The original picture comes back under the eraser. |
| S-06 | Add text | Choose Add text, type something, set a size and colour, click the picture. Also try with no text. | Text appears where clicked. With no text, nothing changes. |
| S-07 | Undo and clear | Make several edits, Undo a few times, then Clear edit. | Undo steps back one edit at a time. Clear edit returns to the original. |
| S-08 | Grid | Tick Grid and leave it on. Check this one after the upload in S-11. | Grid shows in the editor but not on the keyboard's screen. |
| S-09 | Frame delays | Set a frame's delay, try values below 30 and above 500, and use Use this delay for all frames. | Values stop at 30 and 500. The list shows the new delays. |
| S-10 | Preview | Press Preview, then Stop. | Plays at the set speed, and returns to the selected frame. |
| S-11 | Upload | Upload to keyboard with a small GIF. | Progress bar runs, the bottom bar shows **Animation uploaded.**, and the screen shows the right picture, colours and speed. No stripes or partial image. |
| S-12 | Timing check *(Report what you see)* | Upload 10 frames at 100 ms and time the loop. | Loop takes about 1 second. **What did you measure?** |
| S-13 | Upload with nothing | Delete all frames and press Upload. | Red message about 0 frames. The keyboard's screen is unchanged. |
| S-14 | Stored on the keyboard | Exit the app, then unplug and replug. | The animation comes back without the app. |
| S-15 | Frames survive restart | Restart the app. | Frames and delays are still listed. Clear edit now returns to the last saved version. |
| S-16 | Export GIF | Export GIF and open it in a viewer, then Import it. | It plays correctly with the right timing. Colours may be slightly reduced. |
| S-17 | 31 frames | Upload a GIF of 31 or more frames (about 40 seconds). | All frames play, with no errors. Another project limits this to 30 on a different firmware. |
| S-18 | 140 frames *(Report what you see)* | Upload 140 frames (about 2.5 minutes). Use the app's other pages during it. | All frames play and the app stays responsive. **This is the most likely screen test to fail,** so note what happens. |
| S-19 | Over the limit | Import a GIF with more than 140 frames. | Message explains the limit, and only 140 frames are added. |
| S-20 | Unplug during upload | Start an upload, then unplug partway. Replug and upload again. | Red error, no crash. The screen may be blank or partial until the second upload, which should work. |
| S-21 | Time Sync *(Report what you see)* | If your screen has a clock view, press Time Sync. | Bottom bar confirms and the clock shows the right date and time. **Does it keep time after unplugging for a minute?** |

## Stage 7: Keyboard settings

**Overwrites all settings on the keyboard at once, and they can't be read back.** Each write sets every value shown on the page, so check them all first.

| ID | Case | Steps | Expected |
|---|---|---|---|
| C-01 | Key response *(Report what you see)* | Set level 5, Write, and type. Then set level 1, Write, and type. | Both accepted. Level 5 may feel slower, and level 1 should have no chatter (double letters). Note what you notice. |
| C-02 | Disable Windows key | Tick it, Write, press Win. Untick, Write. | Win does nothing when ticked, and works again after. |
| C-03 | Disable Alt+F4 | Tick it, Write, press Alt+F4 on a spare window such as Notepad. Untick, Write. | The window stays open when ticked, and closes after. |
| C-04 | Disable Alt+Tab | Tick it, Write, press Alt+Tab. Untick, Write. | No window switching when ticked, and works again after. |
| C-05 | Fn switch *(Report what you see)* | Set Toggle, Write, and try Fn. Set Momentary, Write, and try again. | **What does each one do?** I couldn't work this out from the vendor program. |
| C-06 | Sleep time *(Report what you see)* | Switch to battery or 2.4 GHz, set 1 min, Write, and leave the keyboard idle. Then set your preferred value. | Keyboard sleeps after about a minute, and wakes on a keypress. It may not sleep on USB power. |
| C-07 | Stored on the keyboard | Set a visible setting (such as Disable Windows key), exit the app, unplug and replug. | Setting still applies. Set it back afterwards. |

## Stage 8: Key remaps

**Overwrites the stored key table for that layer, and it can't be read back.** Test on a spare key. F9 is used in these examples. Press Apply, then Write to keyboard. To undo: Reset this layer, then Write.

| ID | Case | Steps | Expected |
|---|---|---|---|
| K-01 | Editor | Click keys on both layers. | Key name and function list appear. On the Fn layer, Esc, Backspace, Home, Backslash and Left Win are dimmed. Fn is dimmed on both. |
| K-02 | Apply without Write | Set F9 to a custom key, press Apply, then press the real F9. | Key turns blue with a short label, and the bottom bar reminds you to write. The real key is unchanged. |
| K-03 | Simple remap | Set F9 to A. Apply, Write. Press F9 in Notepad. | Types **a**. |
| K-04 | Key with modifiers | Set F9 to Ctrl+Shift+Esc. Write and press. | Task Manager opens. |
| K-05 | Modifier alone | Set F9 to Left Shift. Write. Hold F9 and type a letter. | Letter is capitalized. |
| K-06 | Multi-key *(Report what you see)* | Set F9 to three keys such as Q, W, E. Write. Press in Notepad. | All three type at once, in some order. **Order and any repeat behaviour?** |
| K-07 | Disable | Disable F9. Write. Press. | Nothing happens. |
| K-08 | Mouse functions | Try all 8: left, middle, right, double click, scroll up, scroll down, buttons 4 and 5. | Each does what it says. Double click is the least certain, so check it opens an icon. |
| K-09 | Multimedia | Try all 7: play/pause, stop, previous, next, volume up and down, mute. | Each works with your media player. |
| K-10 | System and web keys *(Report what you see)* | Try all 11: computer, calculator, mail, media player, home, refresh, stop, back, forward, favourites, search. | Each triggers its action. Some depend on your default apps, so note any that do nothing. |
| K-11 | Windows shortcuts | Try all 8: show desktop, computer, lock, close window, switch windows, copy, paste, cut. | Each works. Note that close window sends Ctrl+W. |
| K-12 | Fn layer | On the Fn layer, set F10 to A. Write. Press F10, then Fn+F10. | F10 is normal alone, and Fn+F10 types **a**. |
| K-13 | Layers don't overwrite each other | With K-12 still set, change the top layer and Write it. Retest both. | Both remaps still work. |
| K-14 | Back to default | Set F9 to Default Key. Apply, Write. | F9 works normally. |
| K-15 | Reset layer | Reset this layer, then Write. Do it for both layers. | All blue keys clear and every remap is gone. |
| K-16 | Stored on the keyboard | Make a remap, exit the app, unplug and replug. | Remap still works without the app. Undo it afterwards. |
| K-17 | Open Program | Set F9 to a program such as Notepad. Write and press. Then exit the app and press. | Program opens. With the app closed, nothing happens and no stray character is typed. |
| K-18 | Open a file | Set F9 to a document instead of a program. | Opens in its default app. |
| K-19 | Open Website | Try `example.com`, then `https://example.com/`, then `file:///C:/Windows/win.ini`. | The first two open in the browser. The third does not open. |
| K-20 | Send Text *(Report what you see)* | Set text with accents, an emoji and two lines. Press in Notepad. | Text types correctly and your clipboard is unchanged. **Does the line break work?** |
| K-21 | Several PC-side keys | Set three keys to different programs, website and text. Write and press each. | Each runs its own action. Reassign one and check the others still work. |
| K-22 | Switch Profile | **Writes the whole profile.** Make profile B with different lighting, and give both profiles a switch key. Write the keys in each. Press to go there and back. | Lighting changes, and the active profile on Home updates each way. |
| K-23 | Export and import | Export a key layout, Reset this layer, then Import. | Assignments come back. A bad file gives a red message. |
| K-24 | Write whole profile | **Overwrites macros, both key tables, settings and lighting.** Home, Write to keyboard. | Bottom bar shows **Profile written to keyboard.** The keyboard matches the profile. |

## Stage 9: Macros

**Overwrites all macros on the keyboard, and they can't be read back.** Assigning a macro to a key also writes that key table. Keep the mouse handy.

| ID | Case | Steps | Expected |
|---|---|---|---|
| X-01 | Macro list | New, Rename, try an empty name, Delete. | List updates. An empty name is refused with a message. |
| X-02 | Record with timing | Select Record Input Interval Time, press Start Recording, type `abc` slowly, Stop. | Rows show press and release for each key, with delays close to your real timing. |
| X-03 | Record without timing | Select Do Not Record Input Interval Time and record again. | All delays show 10. |
| X-04 | Fixed interval | Select Default Interval, set 50, and record. | All delays show 50. |
| X-05 | Recording the mouse | Record clicks outside the app window, then click Stop. | Outside clicks appear as rows. Clicks on the app, including Stop, don't. |
| X-06 | Holding a key | While recording, hold a key for 2 seconds. | One press and one release, not a stream of repeats. |
| X-07 | Key combos | Record Ctrl+C. | Ctrl press, C press, C release, Ctrl release, in that order. |
| X-08 | Editing | Insert a key and a mouse button at a selected row, Move Up and Down, Modify Delay Time (or double-click), Delete rows. | Each works. Delays below 10 become 10. |
| X-09 | Leaving while recording | Start recording, then switch page. | Recording stops and what was captured is saved. |
| X-10 | Memory counter | Record or insert until the count passes 90%, then 100%. Try to write. Delete the extra afterwards. | Counter turns amber, then red, and over 3584 bytes the write is refused with a message. |
| X-11 | Play Once | Record `hello`. Write the macro. On Keys, set F9 to it with Play Once. Write. Press in Notepad. | Types **hello** once, with the right letters and order. |
| X-12 | Play Multiple Times *(Report what you see)* | Set Play Multiple Times with Loop Count 3. Write the key table. Press. | **Plays 3 times?** These mode values are inferred, so tell me what really happens. |
| X-13 | Stop when pressed again *(Report what you see)* | Set that mode. Write the key table. Press once, then press again. | **Loops until the second press?** |
| X-14 | Release to stop *(Report what you see)* | Set that mode. Write the key table. Hold, then release. | **Plays while held and stops on release?** |
| X-15 | Click to play *(Report what you see)* | Set that mode. Write the key table. Press. | **What does it do, compared with Play Once?** |
| X-16 | Delay accuracy | Make a macro with a 1000 ms delay between two letters. Time it. | About 1 second between letters. |
| X-17 | Mouse in a macro | Macro with a right click. Assign, write, press. | Right-click menu opens. |
| X-18 | Several macros | Assign 3 macros to 3 keys. Write macros and key table. Delete the middle macro, write macros and key table again. Test the keys. | Remaining keys play the right macros, and the deleted macro's key goes back to default. |
| X-19 | Empty macro | Make a macro with no events, placed before another macro. Write both. Test the other macro. | The other macro still plays correctly. |
| X-20 | Long macro | Near the memory limit, write a macro and play it. | Plays through to the end, with nothing cut off. |
| X-21 | Stored on the keyboard | Exit the app, unplug, replug, and press a macro key. | Plays without the app. |
| X-22 | Export and import | Export a macro, Import it, try a bad file. | Copy matches. A bad file gives a red message. |

## Stage 10: 2.4 GHz and Bluetooth

**Same overwrites as the matching USB features.** None of the wireless path has run on real hardware yet, so failures here are likely and useful. Set the keyboard to 2.4 GHz and unplug the cable.

| ID | Case | Steps | Expected |
|---|---|---|---|
| W-01 | Detection | Keyboard in 2.4 GHz with the dongle in and USB unplugged. | Home shows **AK820 Pro (2.4 GHz)**. |
| W-02 | Battery *(Report what you see)* | Check Home and compare with the keyboard's own indicator. | Shows **Battery N%**, close to the real level. Note what you see. |
| W-03 | Lighting | Change mode, colour, brightness and speed. | Keyboard follows. Note any red errors. |
| W-04 | Per-key colours | Write a pattern. Check the row colours. | Keys match the drawing. |
| W-05 | Key remap | Remap F9 and write. Do the Fn layer too. | Remaps work. |
| W-06 | Macros | Write a macro and assign it. | Plays correctly. |
| W-07 | Settings | Change Disable Windows Key, write it, then change it back. | Takes effect. |
| W-08 | Clock *(Report what you see)* | Press Time Sync. | Clock is correct if your screen shows one over 2.4 GHz. |
| W-09 | Real-time blocked | Press Start on Real-time. | Red message **Real-time lighting needs the USB cable.** |
| W-10 | Screen blocked | Upload on TFT Screen. | Red message that it needs the USB cable. Screen unchanged. |
| W-11 | Music *(Report what you see)* | Play music, press Start. | **Do the keys react?** The firmware draws this over 2.4 GHz, and my values are a best guess. |
| W-12 | PC-side keys | Set a key to Open Website. Press it. | Website opens. |
| W-13 | Keyboard asleep | Let it sleep, then change lighting. Wake it and retry. | Readable message while asleep, no hang or crash. Works after waking. |
| W-14 | Keyboard off | Turn it off with the dongle in, then change lighting. | Red message about no reply, with no hang. Turn it on and it works again. |
| W-15 | Cable while in 2.4 GHz *(Report what you see)* | Plug in USB while still set to 2.4 GHz. | **Which connection does Home show, and do writes work?** |
| W-16 | Bluetooth | Switch to Bluetooth with the dongle removed and USB unplugged. | Home shows **No keyboard found**. No crash. |

## Stage 11: Resets

**Destructive. Leave these until last.** Resets clear macros, remaps and settings on the keyboard. Factory Reset also deletes the profiles and macros saved in this app. Export anything you want first.

| ID | Case | Steps | Expected |
|---|---|---|---|
| Z-01 | Cancel | Press Reset Keyboard and Factory Reset, cancelling each. | Nothing changes. |
| Z-02 | Reset Keyboard | Press Reset Keyboard and confirm. | Keyboard returns to Rolling lighting, default keys and no macros. The app's profiles stay. |
| Z-03 | Restoring afterwards | After Z-02, Home, Write to keyboard. | Your profile returns to the keyboard. |
| Z-04 | Factory Reset | Press Factory Reset and confirm. | Keyboard is at defaults. The app has one Default profile and no macros. Screen frames in the editor remain. |
| Z-05 | Wireless reset *(Report what you see)* | Optional. Repeat Z-02 over 2.4 GHz. | Same result as USB. Note any errors. |
