namespace BlueMarsh.Tandoku;

using System.Drawing;

public interface IHasBoundingBox
{
    /// <summary>
    /// Gets the bounding box dimensions as four (x, y) coordinate pairs.
    /// </summary>
    /// <remarks>
    /// BoundingBox[0] : top-left X
    /// BoundingBox[1] : top-left Y
    /// BoundingBox[2] : top-right X
    /// BoundingBox[3] : top-right Y
    /// BoundingBox[4] : bottom-left X
    /// BoundingBox[5] : bottom-left Y
    /// BoundingBox[6] : bottom-right X
    /// BoundingBox[7] : bottom-right Y
    /// </remarks>
    int[] BoundingBox { get; }
}

public static class BoundingBoxExtensions
{
    // NOTE: this currently ignores any skew in the bounding box (using only top-left and bottom-right coordinates),
    // may need to adjust for this if text has a non-trivial angle
    public static Rectangle ToRectangle(this IHasBoundingBox boundingBox) => Rectangle.FromLTRB(
        boundingBox.BoundingBox[0],
        boundingBox.BoundingBox[1],
        boundingBox.BoundingBox[6],
        boundingBox.BoundingBox[7]);
}
