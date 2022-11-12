using Game.Components;
using Game.Data;

namespace Sculptures.Components
{
	public interface ISculptingProvider : IComponent, ISlots
	{
		int WorkSpot { get; }

		bool Available { get; }

		void BeginSculpting(Being worker);

		bool Sculpt(Being worker);

		void EndSculpting(Being worker);
	}
}
