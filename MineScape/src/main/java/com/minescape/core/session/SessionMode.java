package com.minescape.core.session;

public enum SessionMode {
    FAMILY,
    STEWARD,
    MINEJAMMER,
    /** Disposable MineJammer-only mode which exercises heart rules without family exploration authority. */
    HEART_QA
}
