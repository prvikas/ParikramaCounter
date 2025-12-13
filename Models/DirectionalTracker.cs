using System;
using System.Collections.Generic;
using System.Linq;

namespace ParikramaCounter.Models
{
    public class DirectionalTracker
    {
        private Dictionary<Direction, double> directionalDistance = new Dictionary<Direction, double>();
        private Dictionary<Direction, int> directionalSteps = new Dictionary<Direction, int>();

        private Direction lastDirection = Direction.North;
        private double lastHeading = 0;

        public enum Direction
        {
            North = 0,
            NorthEast = 45,
            East = 90,
            SouthEast = 135,
            South = 180,
            SouthWest = 225,
            West = 270,
            NorthWest = 315
        }

        public enum PathShape
        {
            Unknown,
            Circle,
            Square,
            Rectangle
        }

        public DirectionalTracker()
        {
            Reset();
        }

        public void Update(double currentHeading, bool isMoving)
        {
            if (!isMoving) return;

            Direction currentDirection = GetDirectionFromHeading(currentHeading);
            double distanceMoved = 0.7;

            directionalDistance[currentDirection] += distanceMoved;
            directionalSteps[currentDirection]++;

            lastDirection = currentDirection;
            lastHeading = currentHeading;
        }

        public Direction GetDirectionFromHeading(double heading)
        {
            double normalizedHeading = (heading + 22.5) % 360;
            int sector = (int)(normalizedHeading / 45);

            return sector switch
            {
                0 => Direction.North,
                1 => Direction.NorthEast,
                2 => Direction.East,
                3 => Direction.SouthEast,
                4 => Direction.South,
                5 => Direction.SouthWest,
                6 => Direction.West,
                7 => Direction.NorthWest,
                _ => Direction.North
            };
        }

        public PathShape DetectPathShape()
        {
            double northDist = GetDistanceInDirection(Direction.North);
            double eastDist = GetDistanceInDirection(Direction.East);
            double southDist = GetDistanceInDirection(Direction.South);
            double westDist = GetDistanceInDirection(Direction.West);

            double neDist = GetDistanceInDirection(Direction.NorthEast);
            double seDist = GetDistanceInDirection(Direction.SouthEast);
            double swDist = GetDistanceInDirection(Direction.SouthWest);
            double nwDist = GetDistanceInDirection(Direction.NorthWest);

            double cardinalTotal = northDist + eastDist + southDist + westDist;
            double diagonalTotal = neDist + seDist + swDist + nwDist;
            double total = cardinalTotal + diagonalTotal;

            if (total < 5) return PathShape.Unknown;

            double cardinalRatio = cardinalTotal / total;
            double diagonalRatio = diagonalTotal / total;

            if (cardinalRatio > 0.70)
            {
                double nsAvg = (northDist + southDist) / 2.0;
                double ewAvg = (eastDist + westDist) / 2.0;

                if (Math.Abs(nsAvg - ewAvg) < 3.0)
                {
                    return PathShape.Square;
                }
                else
                {
                    return PathShape.Rectangle;
                }
            }
            else if (diagonalRatio > 0.25)
            {
                return PathShape.Circle;
            }

            return PathShape.Unknown;
        }

        public bool IsValidPath()
        {
            PathShape shape = DetectPathShape();
            System.Diagnostics.Debug.WriteLine($"🔍 Detected shape: {shape}");

            return shape switch
            {
                PathShape.Square or PathShape.Rectangle => ValidateSquareRectangle(),
                PathShape.Circle => ValidateCircle(),
                _ => GetCoveredDirectionCount() >= 4
            };
        }

        private bool ValidateSquareRectangle()
        {
            bool hasNorth = GetDistanceInDirection(Direction.North) >= 2.0;
            bool hasEast = GetDistanceInDirection(Direction.East) >= 2.0;
            bool hasSouth = GetDistanceInDirection(Direction.South) >= 2.0;
            bool hasWest = GetDistanceInDirection(Direction.West) >= 2.0;

            int cardinalCount = (hasNorth ? 1 : 0) + (hasEast ? 1 : 0) +
                               (hasSouth ? 1 : 0) + (hasWest ? 1 : 0);

            if (cardinalCount >= 4)
            {
                System.Diagnostics.Debug.WriteLine($"✅ Valid square/rectangle: all 4 cardinal directions covered");
                return true;
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"❌ Incomplete: only {cardinalCount}/4 cardinal directions");
                return false;
            }
        }

        private bool ValidateCircle()
        {
            int covered = GetCoveredDirectionCount();
            bool valid = covered >= 6;

            System.Diagnostics.Debug.WriteLine(valid
                ? $"✅ Valid circle: {covered}/8 directions"
                : $"❌ Incomplete circle: {covered}/8 directions");

            return valid;
        }

        public double GetDistanceInDirection(Direction direction)
        {
            return directionalDistance.ContainsKey(direction) ? directionalDistance[direction] : 0;
        }

        public int GetStepsInDirection(Direction direction)
        {
            return directionalSteps.ContainsKey(direction) ? directionalSteps[direction] : 0;
        }

        public double GetTotalDistance() => directionalDistance.Values.Sum();
        public int GetTotalSteps() => directionalSteps.Values.Sum();

        public int GetCoveredDirectionCount()
        {
            return Enum.GetValues(typeof(Direction))
                .Cast<Direction>()
                .Count(dir => GetDistanceInDirection(dir) >= 1.0);
        }

        public double GetDirectionCoveragePercentage()
        {
            return (GetCoveredDirectionCount() / 8.0) * 100.0;
        }

        public Dictionary<string, double> GetDirectionalDistances()
        {
            return Enum.GetValues(typeof(Direction))
                .Cast<Direction>()
                .ToDictionary(dir => dir.ToString(), dir => GetDistanceInDirection(dir));
        }

        public string GetMissingDirections()
        {
            var missing = Enum.GetValues(typeof(Direction))
                .Cast<Direction>()
                .Where(dir => GetDistanceInDirection(dir) < 1.0)
                .Select(dir => dir.ToString());

            return missing.Any() ? string.Join(", ", missing) : "None";
        }

        public void Reset()
        {
            directionalDistance.Clear();
            directionalSteps.Clear();

            foreach (Direction dir in Enum.GetValues(typeof(Direction)))
            {
                directionalDistance[dir] = 0;
                directionalSteps[dir] = 0;
            }

            lastHeading = 0;
        }

        public void LogDirectionalStats()
        {
            PathShape shape = DetectPathShape();

            System.Diagnostics.Debug.WriteLine("=== Directional Statistics ===");
            System.Diagnostics.Debug.WriteLine($"Shape: {shape}");
            System.Diagnostics.Debug.WriteLine("\nCardinal:");
            LogDirection(Direction.North);
            LogDirection(Direction.East);
            LogDirection(Direction.South);
            LogDirection(Direction.West);
            System.Diagnostics.Debug.WriteLine("\nDiagonal:");
            LogDirection(Direction.NorthEast);
            LogDirection(Direction.SouthEast);
            LogDirection(Direction.SouthWest);
            LogDirection(Direction.NorthWest);
            System.Diagnostics.Debug.WriteLine($"\nCoverage: {GetDirectionCoveragePercentage():F0}% ({GetCoveredDirectionCount()}/8)");
            System.Diagnostics.Debug.WriteLine($"Total: {GetTotalDistance():F1}m ({GetTotalSteps()} steps)");
        }

        private void LogDirection(Direction dir)
        {
            double dist = GetDistanceInDirection(dir);
            int steps = GetStepsInDirection(dir);
            string status = dist >= 1.0 ? "✅" : "❌";
            System.Diagnostics.Debug.WriteLine($"  {status} {dir,-12}: {dist:F1}m ({steps} steps)");
        }
    }
}
