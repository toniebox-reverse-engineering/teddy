# Test fixtures

Synthetic sine tones (no copyrighted audio), generated with:

```sh
cd album
ffmpeg -f lavfi -i "sine=frequency=440:duration=2:sample_rate=44100"  -ac 2 -c:a libmp3lame -b:a 64k -id3v2_version 3 -metadata track=1 -metadata title="Sine 440" -metadata artist="Teddy Tests" "01 stereo44k.mp3"
ffmpeg -f lavfi -i "sine=frequency=1000:duration=2:sample_rate=22050" -ac 1 -c:a libmp3lame -b:a 32k -id3v2_version 3 -metadata track=2 -metadata title="Mono 1k" -metadata artist="Teddy Tests" "02 MONO22K.MP3"
ffmpeg -f lavfi -i "sine=frequency=660:duration=2:sample_rate=48000"  -ac 2 -c:a libopus    -b:a 48k "03 opus48k.ogg"
echo "not audio" > notes.txt
```

They cover: 44.1 kHz stereo MP3 (resampling), 22.05 kHz mono MP3 with an upper-case
extension (resampling + upmix + case-insensitive discovery), Opus/Ogg input, and a
non-audio file that must be ignored.
