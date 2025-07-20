using UnityEngine;

public struct BVHBox
{
    public enum DisplayType
    {
        Wire , Focus , Recursion
    }

    public int nodeIndex;
    public Color color;
    public DisplayType type;

    public BVHBox(int _nodeIndex , int _depth , DisplayType _type)
    {
        nodeIndex = _nodeIndex;
        color = Color.HSVToRGB(_depth / 6f % 1 , 1 , 1);
        type = _type;

        switch (type)
        {
            case DisplayType.Wire:
                color.a = 0.6f;
                break;
            case DisplayType.Focus:
                color.a = 0.3f;
                break;
            case DisplayType.Recursion:
                color.a = _depth / 10f * 0.1f + 0.02f;
                break;
        }
    } 
}