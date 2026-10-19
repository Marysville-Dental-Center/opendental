using System;
using System.IO;
using System.Media;

namespace CodeBase {
	public class SoundHelper {

		/// <summary>Plays the sound asynchronously using SoundPlayer.Play().</summary>
		public static void PlaySound(byte[] rawData) {
			using MemoryStream stream=new MemoryStream(rawData);
			using SoundPlayer simpleSound = new SoundPlayer(stream);
			simpleSound.Play();
		}

		/// <summary>Plays the sound synchronously using SoundPlayer.PlaySync().</summary>
		public static void PlaySoundSync(byte[] rawData) {
			using MemoryStream stream=new MemoryStream(rawData);
			using SoundPlayer simpleSound = new SoundPlayer(stream);
			simpleSound.PlaySync();
		}

	}
}
