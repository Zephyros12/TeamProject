namespace TeamProject.Models;

public class Defect
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public override string ToString() =>
        $"X: {X}, Y: {Y}, W: {Width}, H: {Height}";
}
