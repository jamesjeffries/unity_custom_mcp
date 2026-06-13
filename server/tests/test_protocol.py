"""Unit tests for the wire protocol framing."""

from __future__ import annotations

import struct

import pytest

from unity_mcp.protocol import (
    ProtocolError,
    Request,
    Response,
    decode_body,
    decode_header,
    encode_frame,
)


def test_encode_frame_round_trips() -> None:
    payload = {"id": "abc", "command": "ping", "params": {"x": 1}}
    frame = encode_frame(payload)

    length = decode_header(frame[:4])
    assert length == len(frame) - 4
    assert decode_body(frame[4 : 4 + length]) == payload


def test_encode_frame_prefix_is_big_endian() -> None:
    frame = encode_frame({})
    (length,) = struct.unpack(">I", frame[:4])
    assert length == len(frame) - 4


def test_decode_header_rejects_wrong_size() -> None:
    with pytest.raises(ProtocolError):
        decode_header(b"\x00\x00")


def test_decode_body_rejects_non_object() -> None:
    with pytest.raises(ProtocolError):
        decode_body(b"[1, 2, 3]")


def test_decode_body_rejects_invalid_json() -> None:
    with pytest.raises(ProtocolError):
        decode_body(b"not json")


def test_request_create_has_unique_ids() -> None:
    a = Request.create("ping")
    b = Request.create("ping")
    assert a.id != b.id
    assert a.to_dict()["command"] == "ping"
    assert a.to_dict()["params"] == {}


def test_response_from_dict_defaults() -> None:
    resp = Response.from_dict({"id": "x", "success": True, "data": {"ok": 1}})
    assert resp.success is True
    assert resp.data == {"ok": 1}

    err = Response.from_dict({"id": "y", "success": False, "error": "boom"})
    assert err.success is False
    assert err.error == "boom"
