import CoreGraphics
import Foundation

/// Where the pointer is relative to the wheel.
public enum WheelHit: Equatable {
    case slice(Int)
    case hub
    case none
}

/// Pure geometry of the 8-slice wheel. Slice 0 sits at 12 o'clock, then clockwise.
public enum WheelGeometry {
    public static let size: CGFloat = 440
    public static let innerRadius: CGFloat = 72
    public static let outerRadius: CGFloat = 168
    public static let hubRadius: CGFloat = 62
    public static let sliceCount = 8

    /// `point` is in wheel coordinates, origin top-left (SwiftUI space).
    public static func hit(at point: CGPoint) -> WheelHit {
        let dx = point.x - size / 2
        let dy = point.y - size / 2
        let distance = hypot(dx, dy)
        if distance <= hubRadius { return .hub }
        // Generous outer edge so a fast flick past the rim still selects the slice.
        if distance >= innerRadius - 6 && distance <= outerRadius + 28 {
            return .slice(sliceIndex(dx: dx, dy: dy))
        }
        return .none
    }

    public static func sliceIndex(dx: CGFloat, dy: CGFloat) -> Int {
        var degrees = atan2(dy, dx) * 180 / .pi + 90 + 22.5
        degrees = degrees.truncatingRemainder(dividingBy: 360)
        if degrees < 0 { degrees += 360 }
        return Int(degrees / 45) % sliceCount
    }

    /// Angle (degrees, SwiftUI orientation) of the middle of a slice.
    public static func centerAngle(of index: Int) -> Double { -90 + Double(index) * 45 }

    /// A point at the middle of a slice, in wheel coordinates. Handy for tests and the self-test.
    public static func point(forSlice index: Int) -> CGPoint {
        let radians = centerAngle(of: index) * .pi / 180
        let mid = (innerRadius + outerRadius) / 2
        return CGPoint(x: size / 2 + cos(radians) * mid, y: size / 2 + sin(radians) * mid)
    }
}
