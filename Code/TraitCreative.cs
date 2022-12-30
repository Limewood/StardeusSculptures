using Sculptures;
using Game.AI.Traits;
using Game.Constants;
using Game.Data;
using KL.Utils;
using UnityEngine;

namespace Sculptures.AI.Traits
{
	public sealed class TraitCreative : Trait
	{
		public const string Id = "creative";

		public override float Rarity => 0.5f;

		protected override string Icon => "Icons/Color/Creative";

		public static float SkillChanceAdd => 0.3f;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register()
		{
			Trait.Add(Id, new TraitCreative());
		}

		public override bool IsCompatibleWith(Being being)
		{
			return being.Skills.GetSkill(SculpturesMod.SkillIdArtistic) != null;
		}

		private TraitCreative()
		{
		}
	}
}
