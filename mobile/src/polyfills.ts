type FindPredicate<T> = (value: T, index: number, array: T[]) => unknown;

type CompatibleArrayPrototype = {
  findLast?: <T>(this: T[], predicate: FindPredicate<T>, thisArg?: unknown) => T | undefined;
  findLastIndex?: <T>(this: T[], predicate: FindPredicate<T>, thisArg?: unknown) => number;
};

const arrayPrototype = Array.prototype as CompatibleArrayPrototype;

// Expo Router 57 uses these ES2023 methods while handling tab and stack history.
// Some Android Hermes builds do not provide them, causing the first navigation
// action to fail with "undefined is not a function".
if (typeof arrayPrototype.findLast !== "function") {
  Object.defineProperty(Array.prototype, "findLast", {
    configurable: true,
    writable: true,
    value<T>(this: T[], predicate: FindPredicate<T>, thisArg?: unknown): T | undefined {
      if (this == null) throw new TypeError("Array.prototype.findLast called on null or undefined");
      if (typeof predicate !== "function") throw new TypeError("predicate must be a function");

      for (let index = this.length - 1; index >= 0; index -= 1) {
        const value = this[index] as T;
        if (predicate.call(thisArg, value, index, this)) return value;
      }
      return undefined;
    }
  });
}

if (typeof arrayPrototype.findLastIndex !== "function") {
  Object.defineProperty(Array.prototype, "findLastIndex", {
    configurable: true,
    writable: true,
    value<T>(this: T[], predicate: FindPredicate<T>, thisArg?: unknown): number {
      if (this == null) throw new TypeError("Array.prototype.findLastIndex called on null or undefined");
      if (typeof predicate !== "function") throw new TypeError("predicate must be a function");

      for (let index = this.length - 1; index >= 0; index -= 1) {
        if (predicate.call(thisArg, this[index] as T, index, this)) return index;
      }
      return -1;
    }
  });
}
