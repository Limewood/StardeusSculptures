using Sculptures.Constants;
using System.Collections.Generic;
using Game;
using Game.Data;
using UnityEngine;

namespace Sculptures {
    public sealed class SculpturesMod {

		public static readonly int SlotDesignationSculpting = Animator.StringToHash("Sculpting");

		public static readonly int SkillIdArtistic = Animator.StringToHash("Artistic");

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register()
		{
			// The.SysSig.GameIdChanged.AddListener((_) => OnInitialize());
			Ready.WhenCore(delegate
			{
				SculptureQualities.LoadVariations();
			});
		}

		private static void OnInitialize() {
			//
		}
    }
}