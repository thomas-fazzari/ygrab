const unitFormat = (unit: string) =>
  new Intl.NumberFormat("en", {
    style: "unit",
    unit,
    unitDisplay: "short",
    maximumFractionDigits: 1,
  });

const BYTES = { size: 1, format: unitFormat("byte") };
const LARGER_SIZES = [
  { size: 1e3, format: unitFormat("kilobyte") },
  { size: 1e6, format: unitFormat("megabyte") },
  { size: 1e9, format: unitFormat("gigabyte") },
  { size: 1e12, format: unitFormat("terabyte") },
];

/** Formats a byte count in the largest decimal unit it reaches (e.g. `1.2 GB`). */
export function formatBytes(bytes: number) {
  const scale = LARGER_SIZES.findLast(({ size }) => bytes >= size) ?? BYTES;
  return scale.format.format(bytes / scale.size);
}
