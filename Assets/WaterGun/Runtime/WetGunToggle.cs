namespace SurfaceWetness.WaterGun {
using UnityEngine.InputSystem;

// Observe key levels: the host may consume InputSystem's frame flags first.
public sealed class WetGunToggle
{
    private Key previousKey;
    private bool previousDown, blocked;
    public void Cancel() { blocked=true; }
    public bool UpdateButton(Key key, bool down, bool allowed)
    {
        if(key!=previousKey)
        {
            previousKey=key; previousDown=down; blocked=down;
            return false;
        }
        bool pressed=down&&!previousDown;
        previousDown=down;
        if(!down)blocked=false;
        if(!allowed) { if(down)blocked=true; return false; }
        return pressed&&!blocked;
    }
}

}
