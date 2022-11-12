using System;
using Game.Utils;
using UnityEngine;

namespace Sculptures.Constants
{
	[Serializable]
	public class SculptureType
	{
		public string Id;

		private string nameT;

		public string NameT => nameT ?? (nameT = ("sculpture.type." + Id.ToLower()).T());
	}
}
