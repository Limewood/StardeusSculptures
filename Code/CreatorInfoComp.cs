using System.Collections.Generic;
using System.Text;
using Game.Components;
using Game.Constants;
using Game.Data;
using Game.Utils;
using KL.Text;
using KL.Utils;
using UnityEngine;

namespace Sculptures.Components {
	public sealed class CreatorInfoComp : BaseComponent<CreatorInfoComp>, IUIDataProvider, IRelocatable, IUISubmenuProvider {
        private int[] creatorIds;
		private string[] creatorNames;
        private UDB dataBlock;

		public bool HasSubmenuNow => true;

		public string SubmenuTitle => "creator.creators".T();

		[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
		private static void Register() {
			BaseComponent<CreatorInfoComp>.AddComponentPrototype(new CreatorInfoComp());
		}

        protected override void OnConfig() {
        }

        public override void OnSave() {
            var data = GetOrCreateData();
            data.SetIntSet("CreatorIds", creatorIds);
            data.SetStringSet("CreatorNames", creatorNames);
        }

        protected override void OnLoad(ComponentData data) {
            creatorIds = data.GetIntSet("CreatorIds", null);
            creatorNames = data.GetStringSet("CreatorNames", null);
        }

        public UDB GetUIBlock() {
            dataBlock ??= UDB.Create(this,
                UDBT.IText,
                IconId.CPersona,
                "creator.title".T(GetCreatorTitle())).WithGroupId(UDBGH.Stats);
            UpdateUIBlock(false);
            return dataBlock;
        }

        private void UpdateUIBlock(bool wasUpdated) {
            if (wasUpdated && dataBlock?.IsShowing != true) { return; }

            dataBlock.UpdateTitle("creator.title".T(GetCreatorTitle()));
        }

        public void GetUIDetails(List<UDB> res) {
            if (creatorIds != null && creatorIds.Length > 1) {
                int i=0;
                foreach (int id in creatorIds) {
                    Being being = S.Beings.Find(id);
                    UDB creatorLine = UDB.Create("slot", UDBT.IText, being?.Definition?.Preview ?? "Icons/Color/Skull", being?.Persona?.Name ?? creatorNames[i]);
                    if (being != null) {
                        creatorLine.WithIconClickFunction(delegate {
                            being.S.Sig.SelectEntity.Send(being);
                        }).WithIconTint(being.UITint)
                            .WithIconHoverFunction(delegate(bool selected) {
                                EntityUtils.ShowEntityLink(null, being, selected);
                            });
                    }
                    res.Add(creatorLine);
                    i++;
                }
            }
        }

		public void SetCreators(int[] ids, string[] names) {
            creatorIds = ids;
			creatorNames = names;
		}

        public void RelocateTo(Entity target) {
			CreatorInfoComp creatorInfoComp = target.GetComponent<CreatorInfoComp>();
			if (creatorInfoComp == null) return;
            creatorInfoComp.creatorIds = creatorIds;
            creatorInfoComp.creatorNames = creatorNames;
        }

        private string GetCreatorTitle() {
            if (creatorIds == null) {
                return "creator.unknown".T();
            } else if (creatorIds.Length == 1) {
                return S.Beings.Find(creatorIds[0])?.Persona?.Name ?? creatorNames[0];
            } else {
                return "creator.names.multiple".T();
            }
        }

		public override string ToString() {
			return $" * CreatorInfoComp [{GetCreatorTitle()}]";
		}
	}
}