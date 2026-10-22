import SwiftUI
import WhirlCore

/// An annular sector with rounded-looking gaps between neighbours.
struct SliceShape: Shape {
    var centerAngle: Double
    var span: Double = 45
    var gap: Double = 2.6
    var inner: CGFloat
    var outer: CGFloat

    func path(in rect: CGRect) -> Path {
        let c = CGPoint(x: rect.midX, y: rect.midY)
        let start = Angle(degrees: centerAngle - span / 2 + gap)
        let end = Angle(degrees: centerAngle + span / 2 - gap)
        var p = Path()
        p.addArc(center: c, radius: outer, startAngle: start, endAngle: end, clockwise: false)
        p.addArc(center: c, radius: inner, startAngle: end, endAngle: start, clockwise: true)
        p.closeSubpath()
        return p
    }
}

/// Real behind-window blur for the glass disc.
struct GlassBlur: NSViewRepresentable {
    var dark: Bool
    func makeNSView(context: Context) -> NSVisualEffectView {
        let v = NSVisualEffectView()
        v.blendingMode = .behindWindow
        v.state = .active
        return v
    }
    func updateNSView(_ v: NSVisualEffectView, context: Context) {
        v.material = .hudWindow
        v.appearance = NSAppearance(named: dark ? .vibrantDark : .vibrantLight)
    }
}

struct WheelView: View {
    @ObservedObject var state: WheelState
    @ObservedObject var settings = Settings.shared
    /// Off only for offline snapshots, which can't draw the live blur.
    var showsBlur = true

    private let size = WheelGeometry.size
    private let inner = WheelGeometry.innerRadius
    private let outer = WheelGeometry.outerRadius

    var body: some View {
        let theme = settings.theme
        ZStack {
            if showsBlur {
                GlassBlur(dark: theme.isDark)
                    .frame(width: outer * 2 + 24, height: outer * 2 + 24)
                    .clipShape(Circle())
            }
            Circle()
                .fill(theme.glass)
                .frame(width: outer * 2 + 24, height: outer * 2 + 24)
                .overlay(Circle().stroke(theme.accent.opacity(0.35), lineWidth: 1))
                .shadow(color: .black.opacity(0.25), radius: 18, y: 8)

            ForEach(0..<WheelGeometry.sliceCount, id: \.self) { index in
                slice(index, theme: theme)
            }

            hub(theme: theme)

            VStack {
                Text(state.mode == .apps ? "APPS" : "WINDOWS")
                    .font(.system(size: 11, weight: .heavy, design: .rounded))
                    .tracking(1.6)
                    .foregroundStyle(theme.isDark ? Color.white : theme.text)
                    .padding(.horizontal, 12).padding(.vertical, 5)
                    .background(Capsule().fill(theme.petal))
                    .overlay(Capsule().stroke(theme.accent.opacity(0.4), lineWidth: 1))
                Spacer()
                if state.pageCount > 1 {
                    HStack(spacing: 6) {
                        ForEach(0..<state.pageCount, id: \.self) { page in
                            Capsule()
                                .fill(page == state.pageIndex ? theme.accent : Color.gray.opacity(0.45))
                                .frame(width: page == state.pageIndex ? 18 : 6, height: 6)
                        }
                    }
                    .padding(6)
                    .background(Capsule().fill(theme.petal))
                }
            }
            .padding(.vertical, 6)
        }
        .frame(width: size, height: size)
        .animation(.spring(response: 0.22, dampingFraction: 0.78), value: state.hit)
        .animation(.spring(response: 0.3, dampingFraction: 0.8), value: state.pageIndex)
    }

    @ViewBuilder
    private func slice(_ index: Int, theme: Theme) -> some View {
        let item = state.pageItems[index]
        let isHovered = state.hit == .slice(index)
        let angle = WheelGeometry.centerAngle(of: index)
        let mid = (inner + outer) / 2
        let radians = angle * .pi / 180

        ZStack {
            SliceShape(centerAngle: angle, inner: inner, outer: outer)
                .fill(isHovered ? theme.accent : theme.petal.opacity(item == nil ? 0.35 : 1))
                .shadow(color: isHovered ? theme.accent.opacity(0.6) : .clear, radius: 12)
            SliceShape(centerAngle: angle, inner: inner, outer: outer)
                .stroke(theme.accent.opacity(isHovered ? 0 : 0.18), lineWidth: 1)

            if let item {
                VStack(spacing: 3) {
                    ZStack(alignment: .bottom) {
                        Image(nsImage: IconCache.icon(for: item))
                            .resizable().interpolation(.high)
                            .frame(width: 36, height: 36)
                            .opacity(item.isRunning ? 1 : 0.55)
                    }
                    Text(item.title)
                        .font(.system(size: 10.5, weight: .bold, design: .rounded))
                        .lineLimit(1)
                        .minimumScaleFactor(0.7)
                    marker(item, isHovered: isHovered, theme: theme)
                }
                .frame(width: 80)
                .foregroundStyle(isHovered ? theme.textOnAccent : theme.text)
                .offset(x: cos(radians) * mid, y: sin(radians) * mid)
            }
        }
        .frame(width: size, height: size)
        .scaleEffect(isHovered ? 1.07 : 1)
    }

    /// A dot for "running", a ring for "you are here". Pinned apps that are closed show nothing.
    @ViewBuilder
    private func marker(_ item: SwitchItem, isHovered: Bool, theme: Theme) -> some View {
        let color = isHovered ? theme.textOnAccent : theme.accent
        if item.isFrontmost {
            Capsule().fill(color).frame(width: 14, height: 4)
        } else if item.isRunning {
            Circle().fill(color.opacity(0.8)).frame(width: 4, height: 4)
        } else {
            Color.clear.frame(width: 4, height: 4)
        }
    }

    @ViewBuilder
    private func hub(theme: Theme) -> some View {
        let isHovered = state.hit == .hub
        let r = WheelGeometry.hubRadius
        ZStack {
            Circle()
                .fill(isHovered ? theme.accent : theme.petal)
                .overlay(Circle().stroke(theme.accent.opacity(0.5), lineWidth: 1.5))
                .shadow(color: isHovered ? theme.accent.opacity(0.6) : .black.opacity(0.12), radius: isHovered ? 14 : 6)

            if let item = state.hoveredItem {
                VStack(spacing: 3) {
                    Text(item.subtitle ?? item.title)
                        .font(.system(size: 12, weight: .heavy, design: .rounded))
                        .foregroundStyle(theme.accent)
                        .lineLimit(item.subtitle == nil ? 2 : 1)
                    Text(item.subtitle == nil ? (item.isRunning ? "Release to switch" : "Release to open")
                                              : item.title)
                        .font(.system(size: 10, weight: .medium))
                        .foregroundStyle(theme.text)
                        .lineLimit(2)
                }
                .multilineTextAlignment(.center)
                .frame(width: r * 1.7)
            } else if state.needsAccessibility {
                VStack(spacing: 3) {
                    Image(systemName: "lock.fill").font(.system(size: 15, weight: .semibold))
                    Text("Grant Accessibility\nin Preferences").font(.system(size: 9, weight: .medium)).multilineTextAlignment(.center)
                }
                .foregroundStyle(theme.text)
                .frame(width: r * 1.7)
            } else {
                VStack(spacing: 4) {
                    Image(systemName: "xmark").font(.system(size: 14, weight: .bold))
                    Text("Cancel").font(.system(size: 10, weight: .bold, design: .rounded))
                }
                .foregroundStyle(isHovered ? theme.textOnAccent : theme.text.opacity(0.75))
                .frame(width: r * 1.7)
            }
        }
        .frame(width: r * 2, height: r * 2)
        .scaleEffect(isHovered ? 1.06 : 1)
    }
}
