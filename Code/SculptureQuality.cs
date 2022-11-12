using System;
using Game.Utils;
using KL.Utils;
using UnityEngine;

namespace Sculptures.Constants
{
	[Serializable]
	public class SculptureQuality
	{
		public string Id;

		private string nameT;

		public string NameT => nameT ?? (nameT = ("sculptures.quality." + Id.ToLower()).T());

		public float Rarity;

		public float[] CraftingChancePoints;

		private Curve craftingChanceCurve;

		public Curve CraftingChanceCurve
		{
			get
			{
				if (craftingChanceCurve == null && CraftingChancePoints != null)
				{
					craftingChanceCurve = CraftingChancePoints;
				}
				return craftingChanceCurve;
			}
		}

		public float Aesthetics;

		public bool HasHeight;
	}
}
