/* Hermes versions bundled on some Android devices do not yet expose ES2023 array helpers. */
if (!Array.prototype.findLast) {
  Object.defineProperty(Array.prototype, 'findLast', {
    configurable: true,
    writable: true,
    value: function findLast(predicate, thisArg) {
      for (let index = this.length - 1; index >= 0; index -= 1) {
        if (predicate.call(thisArg, this[index], index, this)) return this[index];
      }
      return undefined;
    },
  });
}

if (!Array.prototype.findLastIndex) {
  Object.defineProperty(Array.prototype, 'findLastIndex', {
    configurable: true,
    writable: true,
    value: function findLastIndex(predicate, thisArg) {
      for (let index = this.length - 1; index >= 0; index -= 1) {
        if (predicate.call(thisArg, this[index], index, this)) return index;
      }
      return -1;
    },
  });
}
