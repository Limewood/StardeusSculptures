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

namespace Sculptures.AI.Recreation
{
	public sealed class RecSculpt : RecreationActivity
	{
		public const string Id = "RecSculpt";

		protected override string Icon => "Icons/Color/Sculpt";

		protected override string Title => "sculpt".T();

		protected override string ActionType => ActSculpt.ActType;

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register()
		{
			RecreationActivity.Add(Id, new RecSculpt());
		}

		public override bool IsAvailableFor(Being being, out float priority)
		{
			priority = 0f;
			D.Warn("RecSculpt, IsAvailableFor " + being);
			HashSet<int> blacklist = null;
			Slot slot;
			while (true)
			{
				slot = being.S.Sys.Slots.FindForDesignation<ISculptingProvider>(being, SculpturesMod.SlotDesignationSculpting, blacklist, out var obj);
				D.Warn("Slot: " + slot);
				if (slot == null)
				{
					return false;
				}
				// Check if device is available
				D.Warn("Available: " + obj.Available);
				if (!obj.Available)
				{
					if (blacklist == null)
					{
						blacklist = new HashSet<int>();
					}
					blacklist.Add(obj.EntityId);
				} else {
					break;
				}
			}
			priority = 100f - being.Needs.GetNeed(NeedId.Stress).Value;
			priority += 100f - being.Needs.GetNeed(NeedId.Fun).Value;
			// Add to priority if Artistic trait
			if (being.Traits.HasTrait(TraitCreative.Id)) {
				priority += 100f;
			}
			return true;
		}

		protected override void PostProcessAd(Advert ad)
		{
			ad.WithSkillCheck(SculpturesMod.SkillIdArtistic, 1)
				.WithPromises(new Reward[2] {
					new Reward {
						NeedHash = NeedId.Purpose,
						Amount = 5
					},
					new Reward {
						NeedHash = NeedId.Stress,
						Amount = 3
					}
				});
		}
	}
}
