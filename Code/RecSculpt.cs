using Sculptures.AI.Actions;
using Sculptures.AI.Traits;
using Sculptures.Components;
using Sculptures.Constants;
using System.Collections.Generic;
using Game.AI;
using Game.AI.Recreation;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Utils;
using KL.Utils;
using UnityEngine;

namespace Sculptures.AI.Recreation{
	public sealed class RecSculpt : RecreationActivity{
		public const string Id = "RecSculpt";

		protected override string Icon => "Icons/Color/Sculpt";

		protected override string Title => "sculpt".T();

		protected override string ActionType => ActSculpt.ActType;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register() {
			RecreationActivity.Add(Id, new RecSculpt());
		}

		public override bool IsAvailableFor(Being being, out float priority) {
			priority = 0f;
			if (being.S.Sys.Slots.HasFreeFor(SculpturesMod.SlotDesignationSculpting, being)) {
				priority = 100f - being.Needs.GetNeed(NeedIdH.Stress).Value;
				priority += 100f - being.Needs.GetNeed(NeedIdH.Fun).Value;
				// Add to priority if Artistic trait
				if (being.Traits.HasTrait(TraitCreative.Id)) {
					priority += 100f;
				}
				return true;
			}
			return false;
		}

		protected override void PostProcessAd(Advert ad) {
			ad.WithSkillCheck(SculpturesMod.SkillIdArtistic, 1)
				.WithPromises(new Reward[2] {
					new Reward {
						NeedHash = NeedIdH.Purpose,
						Amount = 5
					},
					new Reward {
						NeedHash = NeedIdH.Stress,
						Amount = 3
					}
				});
		}
	}
}
