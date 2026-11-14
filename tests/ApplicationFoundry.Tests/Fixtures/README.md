# Synthetic Access fixtures

These files contain only artificial test records. Do not replace them with an
applicant's database.

- `synthetic-jet4.mdb` was created locally with JackcessDotNet 3.1.0. Its sole
  `Applicants` row is `1, Avery Example, avery@example.test`.
- `synthetic-ace12.accdb` was created locally with Jackcess Java 5.0.4 using
  `Database.FileFormat.V2007`. Jackcess identifies it as ACE 12. Its sole
  `Applicants` row has the same artificial values.
- `synthetic-jet3-jackcess.mdb` is the unmodified artificial
  [`testV1997.mdb`](https://github.com/jahlborn/jackcess/blob/be0a2139b087a227d14cafd4ea231306a5a3a862/src/test/data/V1997/testV1997.mdb)
  fixture from Jackcess. It has generic `Table1` through `Table4` test tables.
  Its SHA-256 is `27788b47e17412427830e80c2cb69366885aa11b12c61422de73029194bef135`.
  Jackcess licenses this fixture under Apache 2.0; copies of its
  [license](JACKCESS-LICENSE.txt) and [notice](JACKCESS-NOTICE.txt) are included.

The application reads these files only in tests. None is packaged into an
installer or holds the owner's records.
