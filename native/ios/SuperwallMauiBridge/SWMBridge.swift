import Foundation
import SuperwallKit

/// Callbacks for a single `register` call. Payloads are JSON strings (docs/architecture.md §4.4).
@objc(SWMPresentationListener)
public protocol SWMPresentationListener: NSObjectProtocol {
  func onPresent(_ paywallInfoJson: String)
  func onDismiss(_ paywallInfoJson: String, resultJson: String)
  func onError(_ message: String)
  func onSkip(_ reasonJson: String)
  func onFeature()
}

/// Receives every Superwall analytics event as `eventJson`.
@objc(SWMEventListener)
public protocol SWMEventListener: NSObjectProtocol {
  func onEvent(_ eventJson: String)
}

/// Receives `SuperwallDelegate` callbacks as `kind` + JSON payload (docs/architecture.md §4.4).
@objc(SWMDelegateListener)
public protocol SWMDelegateListener: NSObjectProtocol {
  func onDelegateEvent(_ kind: String, payloadJson: String)
}

/// Thin ObjC-visible bridge over SuperwallKit for .NET MAUI. All SuperwallKit calls happen on the main thread.
@objc(SWMBridge)
public final class SWMBridge: NSObject {
  @objc public static let shared = SWMBridge()

  @objc public private(set) var isConfigured = false

  @objc public var eventListener: SWMEventListener? {
    didSet { eventDelegate.listener = eventListener }
  }

  @objc public var delegateListener: SWMDelegateListener? {
    didSet { eventDelegate.delegateListener = delegateListener }
  }

  private let eventDelegate = EventDelegate()

  private override init() {
    super.init()
  }

  @objc public func configure(
    apiKey: String,
    optionsJson: String?,
    completion: @escaping (Bool, String?) -> Void
  ) {
    onMain {
      if Superwall.isInitialized {
        completion(self.isConfigured, "Superwall.configure was already called")
        return
      }

      let options = SuperwallOptions()
      let parsed = Self.parseObject(optionsJson) ?? [:]
      if let level = Self.logLevel(parsed["logLevel"] as? String) {
        options.logging.level = level
      }
      if let behavior = Self.parseTestModeBehavior(parsed["testModeBehavior"] as? String) {
        options.testModeBehavior = behavior
      }
      if let locale = parsed["localeIdentifier"] as? String {
        options.localeIdentifier = locale
      }
      if let tracking = Self.parseEventTrackingBehavior(parsed["eventTrackingBehavior"] as? String) {
        options.eventTrackingBehavior = tracking
      }
      if let paywalls = parsed["paywalls"] as? [String: Any] {
        Self.apply(paywalls, to: options.paywalls)
      }

      let superwall = Superwall.configure(apiKey: apiKey, purchaseController: nil, options: options) {
        let status = Superwall.shared.configurationStatus
        self.isConfigured = status == .configured
        completion(self.isConfigured, self.isConfigured ? nil : "Superwall configuration status: \(status)")
      }
      superwall.delegate = self.eventDelegate
    }
  }

  @objc public func register(
    placement: String,
    paramsJson: String?,
    hasFeature: Bool,
    listener: SWMPresentationListener?
  ) {
    onMain {
      guard Superwall.isInitialized else {
        listener?.onError("Superwall is not configured")
        return
      }

      let handler = PaywallPresentationHandler()
      handler.onPresent { info in
        listener?.onPresent(Self.json(info))
      }
      handler.onDismiss { info, result in
        listener?.onDismiss(Self.json(info), resultJson: Self.json(result))
      }
      handler.onError { error in
        listener?.onError(error.localizedDescription)
      }
      handler.onSkip { reason in
        listener?.onSkip(Self.json(reason))
      }

      let params = Self.parseObject(paramsJson)
      if hasFeature {
        Superwall.shared.register(placement: placement, params: params, handler: handler) {
          listener?.onFeature()
        }
      } else {
        Superwall.shared.register(placement: placement, params: params, handler: handler)
      }
    }
  }

  @objc public func dismiss(completion: @escaping () -> Void) {
    onMain {
      guard Superwall.isInitialized else {
        completion()
        return
      }
      Superwall.shared.dismiss {
        DispatchQueue.main.async { completion() }
      }
    }
  }

  @objc public func getSubscriptionStatusJson() -> String {
    guard Superwall.isInitialized else {
      return Self.json(SubscriptionStatus.unknown)
    }
    return Self.json(Superwall.shared.subscriptionStatus)
  }

  @objc public func restorePurchases(completion: @escaping (String) -> Void) {
    onMain {
      guard Superwall.isInitialized else {
        completion(Self.restoreResultJson(.failed(nil)))
        return
      }
      Superwall.shared.restorePurchases { result in
        completion(Self.restoreResultJson(result))
      }
    }
  }

  // MARK: - User

  @objc public func identify(userId: String, restorePaywallAssignments: Bool) {
    onMain {
      guard Superwall.isInitialized else { return }
      Superwall.shared.identify(
        userId: userId,
        options: IdentityOptions(restorePaywallAssignments: restorePaywallAssignments)
      )
    }
  }

  @objc public func reset() {
    onMain {
      guard Superwall.isInitialized else { return }
      Superwall.shared.reset()
    }
  }

  @objc public var userId: String? {
    onMainSync { Superwall.isInitialized ? Superwall.shared.userId : nil }
  }

  @objc public var isLoggedIn: Bool {
    onMainSync { Superwall.isInitialized && Superwall.shared.isLoggedIn }
  }

  @objc public func setUserAttributes(json: String) {
    guard let parsed = Self.parseObject(json) else { return }
    let attributes: [String: Any?] = parsed.mapValues { value -> Any? in value is NSNull ? nil : value }
    onMain {
      guard Superwall.isInitialized else { return }
      Superwall.shared.setUserAttributes(attributes)
    }
  }

  @objc public func getUserAttributesJson() -> String {
    let attributes = onMainSync { Superwall.isInitialized ? Superwall.shared.userAttributes : [:] }
    var result: [String: Any] = [:]
    for (key, value) in attributes {
      result[key] = JSONSerialization.isValidJSONObject([value]) ? value : String(describing: value)
    }
    return Self.serialize(result)
  }

  // MARK: - Presentation control

  @objc public func getPresentationResult(
    placement: String,
    paramsJson: String?,
    completion: @escaping (String) -> Void
  ) {
    onMain {
      guard Superwall.isInitialized else {
        completion(Self.serialize(["type": "error", "error": "Superwall is not configured"]))
        return
      }
      Superwall.shared.getPresentationResult(
        forPlacement: placement,
        params: Self.parseObject(paramsJson)
      ) { result in
        let json = Self.json(result)
        onMain { completion(json) }
      }
    }
  }

  @objc public func preloadAllPaywalls() {
    onMain {
      guard Superwall.isInitialized else { return }
      Superwall.shared.preloadAllPaywalls()
    }
  }

  @objc public func preloadPaywalls(placementsJson: String) {
    guard
      let data = placementsJson.data(using: .utf8),
      let placements = (try? JSONSerialization.jsonObject(with: data)) as? [String]
    else {
      return
    }
    onMain {
      guard Superwall.isInitialized else { return }
      Superwall.shared.preloadPaywalls(forPlacements: Set(placements))
    }
  }

  @objc public var isPaywallPresented: Bool {
    onMainSync { Superwall.isInitialized && Superwall.shared.isPaywallPresented }
  }

  // MARK: - Deep links and runtime settings

  /// Safe before configure: SuperwallKit stores the link until configuration finishes.
  @objc public func handleDeepLink(url: String) -> Bool {
    guard let parsed = URL(string: url) else { return false }
    return onMainSync { Superwall.handleDeepLink(parsed) }
  }

  @objc public func setLogLevel(_ level: String) {
    guard let logLevel = Self.logLevel(level) else { return }
    onMain {
      guard Superwall.isInitialized else { return }
      Superwall.shared.logLevel = logLevel
    }
  }

  // MARK: - Mapping to the JSON contract

  private static func logLevel(_ value: String?) -> LogLevel? {
    switch value {
    case "debug": return .debug
    case "info": return .info
    case "warn": return .warn
    case "error": return .error
    case "none": return LogLevel.none
    default: return nil
    }
  }

  private static func parseTestModeBehavior(_ value: String?) -> SuperwallOptions.TestModeBehavior? {
    switch value {
    case "automatic": return .automatic
    case "whenEnabledForUser": return .whenEnabledForUser
    case "never": return .never
    case "always": return .always
    default: return nil
    }
  }

  private static func parseEventTrackingBehavior(_ value: String?) -> EventTrackingBehavior? {
    switch value {
    case "all": return .all
    case "superwallOnly": return .superwallOnly
    case "none": return EventTrackingBehavior.none
    default: return nil
    }
  }

  private static func apply(_ json: [String: Any], to paywalls: PaywallOptions) {
    if let value = json["isHapticFeedbackEnabled"] as? Bool {
      paywalls.isHapticFeedbackEnabled = value
    }
    if let value = json["shouldShowPurchaseFailureAlert"] as? Bool {
      paywalls.shouldShowPurchaseFailureAlert = value
    }
    if let value = json["shouldPreload"] as? Bool {
      paywalls.shouldPreload = value
    }
    if let value = json["automaticallyDismiss"] as? Bool {
      paywalls.automaticallyDismiss = value
    }
  }

  fileprivate static func json(_ info: PaywallInfo) -> String {
    serialize([
      "identifier": info.identifier,
      "name": info.name,
      "url": info.url.absoluteString,
      "presentedByPlacementWithName": info.presentedByPlacementWithName ?? NSNull(),
      "productIds": info.productIds,
    ])
  }

  private static func json(_ result: PaywallResult) -> String {
    switch result {
    case .purchased(let product):
      return serialize(["type": "purchased", "productId": product.productIdentifier])
    case .declined:
      return serialize(["type": "declined"])
    case .restored:
      return serialize(["type": "restored"])
    @unknown default:
      return serialize(["type": "unknown"])
    }
  }

  fileprivate static func json(_ status: SubscriptionStatus) -> String {
    let activeIds: [String]
    let state: String
    switch status {
    case .unknown:
      activeIds = []
      state = "unknown"
    case .inactive:
      activeIds = []
      state = "inactive"
    case .active(let entitlements):
      activeIds = entitlements.filter(\.isActive).map(\.id).sorted()
      state = activeIds.isEmpty ? "inactive" : "active"
    @unknown default:
      activeIds = []
      state = "unknown"
    }
    return serialize(["status": state, "activeEntitlementIds": activeIds])
  }

  private static func restoreResultJson(_ result: RestorationResult) -> String {
    switch result {
    case .restored:
      return serialize(["type": "restored"])
    case .failed(let error):
      return serialize(["type": "failed", "error": error?.localizedDescription ?? NSNull()])
    @unknown default:
      return serialize(["type": "failed", "error": "Unknown restoration result"])
    }
  }

  private static func json(_ reason: PaywallSkippedReason) -> String {
    let type: String
    switch reason {
    case .holdout: type = "holdout"
    case .noAudienceMatch: type = "noAudienceMatch"
    case .placementNotFound: type = "placementNotFound"
    @unknown default: type = "unknown"
    }
    return serialize(["type": type])
  }

  private static func json(_ result: PresentationResult) -> String {
    switch result {
    case .placementNotFound:
      return serialize(["type": "placementNotFound"])
    case .noAudienceMatch:
      return serialize(["type": "noAudienceMatch"])
    case .paywall(let experiment):
      return serialize(["type": "paywall", "experiment": json(experiment)])
    case .holdout(let experiment):
      return serialize(["type": "holdout", "experiment": json(experiment)])
    case .paywallNotAvailable:
      return serialize(["type": "paywallNotAvailable"])
    @unknown default:
      return serialize(["type": "unknown"])
    }
  }

  private static func json(_ experiment: Experiment) -> [String: Any] {
    [
      "id": experiment.id,
      "groupId": experiment.groupId,
      "variantId": experiment.variant.id,
      "paywallId": experiment.variant.paywallId ?? NSNull(),
    ]
  }

  fileprivate static func json(_ eventInfo: SuperwallEventInfo) -> String {
    var params: [String: Any] = [:]
    for (key, value) in eventInfo.params {
      params[key] = (value is NSNull) ? NSNull() : String(describing: value)
    }
    return serialize(["name": eventInfo.event.description, "params": params])
  }

  private static func parseObject(_ json: String?) -> [String: Any]? {
    guard let data = json?.data(using: .utf8) else { return nil }
    return (try? JSONSerialization.jsonObject(with: data)) as? [String: Any]
  }

  fileprivate static func serialize(_ object: [String: Any]) -> String {
    guard
      let data = try? JSONSerialization.data(withJSONObject: object),
      let string = String(data: data, encoding: .utf8)
    else {
      return "{}"
    }
    return string
  }
}

private final class EventDelegate: SuperwallDelegate {
  weak var listener: SWMEventListener?
  weak var delegateListener: SWMDelegateListener?

  func handleSuperwallEvent(withInfo eventInfo: SuperwallEventInfo) {
    let json = SWMBridge.json(eventInfo)
    onMain { self.listener?.onEvent(json) }
  }

  func subscriptionStatusDidChange(from oldValue: SubscriptionStatus, to newValue: SubscriptionStatus) {
    send("subscriptionStatusDidChange", SWMBridge.json(newValue))
  }

  func handleCustomPaywallAction(withName name: String) {
    send("customPaywallAction", SWMBridge.serialize(["name": name]))
  }

  func willPresentPaywall(withInfo paywallInfo: PaywallInfo) {
    send("willPresentPaywall", SWMBridge.json(paywallInfo))
  }

  func didPresentPaywall(withInfo paywallInfo: PaywallInfo) {
    send("didPresentPaywall", SWMBridge.json(paywallInfo))
  }

  func willDismissPaywall(withInfo paywallInfo: PaywallInfo) {
    send("willDismissPaywall", SWMBridge.json(paywallInfo))
  }

  func didDismissPaywall(withInfo paywallInfo: PaywallInfo) {
    send("didDismissPaywall", SWMBridge.json(paywallInfo))
  }

  func paywallWillOpenURL(url: URL) {
    send("paywallWillOpenUrl", SWMBridge.serialize(["url": url.absoluteString]))
  }

  func paywallWillOpenDeepLink(url: URL) {
    send("paywallWillOpenDeepLink", SWMBridge.serialize(["url": url.absoluteString]))
  }

  private func send(_ kind: String, _ payloadJson: String) {
    onMain { self.delegateListener?.onDelegateEvent(kind, payloadJson: payloadJson) }
  }
}

private func onMain(_ block: @escaping () -> Void) {
  if Thread.isMainThread {
    block()
  } else {
    DispatchQueue.main.async(execute: block)
  }
}

private func onMainSync<T>(_ block: () -> T) -> T {
  if Thread.isMainThread {
    return block()
  }
  return DispatchQueue.main.sync(execute: block)
}
