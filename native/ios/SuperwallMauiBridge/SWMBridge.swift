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

/// Thin ObjC-visible bridge over SuperwallKit for .NET MAUI. All SuperwallKit calls happen on the main thread.
@objc(SWMBridge)
public final class SWMBridge: NSObject {
  @objc public static let shared = SWMBridge()

  @objc public private(set) var isConfigured = false

  @objc public var eventListener: SWMEventListener? {
    didSet { eventDelegate.listener = eventListener }
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
      if let level = Self.parseLogLevel(optionsJson) {
        options.logging.level = level
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

  // MARK: - Mapping to the JSON contract

  private static func parseLogLevel(_ optionsJson: String?) -> LogLevel? {
    switch parseObject(optionsJson)?["logLevel"] as? String {
    case "debug": return .debug
    case "info": return .info
    case "warn": return .warn
    case "error": return .error
    case "none": return LogLevel.none
    default: return nil
    }
  }

  private static func json(_ info: PaywallInfo) -> String {
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

  private static func serialize(_ object: [String: Any]) -> String {
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

  func handleSuperwallEvent(withInfo eventInfo: SuperwallEventInfo) {
    let json = SWMBridge.json(eventInfo)
    onMain { self.listener?.onEvent(json) }
  }
}

private func onMain(_ block: @escaping () -> Void) {
  if Thread.isMainThread {
    block()
  } else {
    DispatchQueue.main.async(execute: block)
  }
}
