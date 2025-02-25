-- CREATE FUNCTION
CREATE FUNCTION IF NOT EXISTS business_application.tx_nextval(
  seq regclass
)
RETURNS bigint AS $$
DECLARE
  seq_id oid;
  next_val bigint;
BEGIN
  -- Make sure seq is a sequence
  SELECT "oid" INTO seq_id
  FROM pg_class
  WHERE "oid" = seq::oid AND "relkind" = 'S';

  IF seq_id IS NULL THEN
    RAISE EXCEPTION 'Relation %s is not a sequence', seq;
  END IF;

  -- Get the last value of the sequence
  SELECT nextval(seq) INTO next_val;

  -- Acquire a advisory lock on the sequence
  PERFORM pg_advisory_xact_lock_shared(seq::int, 0);
  PERFORM pg_advisory_xact_lock_shared(next_val - 1);

  -- Return the next value
  RETURN next_val;
END;
$$ LANGUAGE plpgsql;


-- CREATE FUNCTION
CREATE FUNCTION IF NOT EXISTS business_application.tx_max_safeval(
  seq regclass
)
RETURNS bigint AS $$
DECLARE
  seq_id oid;
  max_seq bigint;
BEGIN
  -- Make sure seq is a sequence
  SELECT "oid" INTO seq_id
  FROM pg_class
  WHERE "oid" = seq::oid AND "relkind" = 'S';

  IF seq_id IS NULL THEN
    RAISE EXCEPTION 'Relation %s is not a sequence', seq;
  END IF;

  -- Find the minimum seq across all running transactions
  -- note: we deal with two related locks here (correlated by pid and virtualtransaction)
  -- one is to know we've locked on the sequence, and the other is to know what value
  -- we've locked all values after
  -- The first one (seq_lock) has classid = the oid of the sequence, and objid = 0
  -- the second one (val_lock) has classid = 0, and objid = the value we've locked
  SELECT min(val_lock.objid) INTO max_seq
  FROM pg_locks seq_lock
  INNER JOIN pg_locks val_lock
    ON  seq_lock.pid = val_lock.pid
    AND seq_lock.virtualtransaction = val_lock.virtualtransaction
  WHERE seq_lock.classid = seq::oid
    AND seq_lock.objid = 0
    AND seq_lock.locktype = 'advisory'
    AND val_lock.classid = 0
    AND val_lock.locktype = 'advisory';

  -- If no locks are found, return the maximum possible bigint value
  IF max_seq IS NULL THEN
      RETURN 9223372036854775807;
  END IF;

  -- Return the maximum safe value
  RETURN max_seq;
END;
$$ LANGUAGE plpgsql;