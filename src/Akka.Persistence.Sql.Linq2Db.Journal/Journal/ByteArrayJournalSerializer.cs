﻿using System;
using System.Collections.Immutable;
using System.Linq;
using Akka.Actor;
using Akka.Configuration;
using Akka.Serialization;
using Akka.Util;

namespace Akka.Persistence.Sql.Linq2Db
{
    public class ByteArrayJournalSerializer : FlowPersistentReprSerializer<JournalRow>
    {
        private Akka.Serialization.Serialization _serializer;
        private string _separator;
        private JournalConfig _journalConfig;

        public ByteArrayJournalSerializer(JournalConfig journalConfig, Akka.Serialization.Serialization serializer, string separator)
        {
            _journalConfig = journalConfig;
            _serializer = serializer;
            _separator = separator;
        }
        protected override Try<JournalRow> Serialize(IPersistentRepresentation persistentRepr, IImmutableSet<string> tTags)
        {

            return Try<JournalRow>.From(() =>
            {
                var serializer = _serializer.FindSerializerForType(persistentRepr.Payload.GetType(),_journalConfig.DefaultSerializer);
                // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                string manifest = "";
                var binary = Akka.Serialization.Serialization.WithTransport(_serializer.System, () =>
                {
                
                    if (serializer is SerializerWithStringManifest stringManifest)
                    {
                        manifest =
                            stringManifest.Manifest(persistentRepr.Payload);
                    }
                    else
                    {
                        if (serializer.IncludeManifest)
                        {
                            manifest = persistentRepr.Payload.GetType().TypeQualifiedName();
                        }
                    }

                    return serializer.ToBinary(persistentRepr.Payload);
                });
                return new JournalRow()
                {
                    manifest = manifest,
                    message = binary,
                    persistenceId = persistentRepr.PersistenceId,
                    tags = tTags.Any()?  tTags.Aggregate((tl, tr) => tl + _separator + tr) : "",
                    Identifier = serializer.Identifier,
                    sequenceNumber = persistentRepr.SequenceNr
                };
            });
        }

        protected override Try<(IPersistentRepresentation, IImmutableSet<string>, long)> deserialize(JournalRow t)
        {
            return Try<(IPersistentRepresentation, IImmutableSet<string>, long)>.From(
                () =>
                {
                    object deserialized = null;
                    if (t.Identifier.HasValue == false)
                    {
                        var type = System.Type.GetType(t.manifest, true);
                        var deserializer =
                            _serializer.FindSerializerForType(type, null);
                        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                        deserialized =
                            Akka.Serialization.Serialization.WithTransport(
                                _serializer.System,
                                () => deserializer.FromBinary(t.message, type));
                    }
                    else
                    {
                        var serializerId = t.Identifier.Value;
                        // TODO: hack. Replace when https://github.com/akkadotnet/akka.net/issues/3811
                        deserialized = _serializer.Deserialize(t.message,
                            serializerId,t.manifest);
                    }

                    return (
                        new Persistent(deserialized, t.sequenceNumber,
                            t.persistenceId,
                            t.manifest, t.deleted, ActorRefs.NoSender, null),
                        t.tags?.Split(new[] {_separator},
                                StringSplitOptions.RemoveEmptyEntries)
                            .ToImmutableHashSet() ?? ImmutableHashSet<string>.Empty,
                        t.ordering);
                }
            );
        }
    }
}